using System.Text;

namespace SymatoIME;

/// <summary>
/// Converts ASCII input to Vietnamese UTF-8 using Telex-like rules
/// </summary>
public class VietnameseConverter
{
    private readonly StringBuilder _buffer = new();
    private readonly StringBuilder _rawBuffer = new();
    private readonly Stack<UndoAction> _undoStack = new();
    private string _renderedText = "";  // Track what's displayed on screen
    private DateTime _lastKeyTime = DateTime.MinValue;
    private const int BufferTimeoutMs = 2000;
    
    // Public settings controlled by tray menu
    public bool AutoIeYeEnabled { get; set; } = true;
    public bool DoubleKeyRawEnabled { get; set; } = true;

    private record UndoAction(int Position, char OldChar, char NewChar, ActionType Type);
    private enum ActionType { Tone, Circumflex, BreveHorn, DToD }

    // Vowel transformations with 'z' (circumflex: â, ê, ô)
    private static readonly Dictionary<char, char> CircumflexMap = new()
    {
        {'a', 'â'}, {'A', 'Â'},
        {'e', 'ê'}, {'E', 'Ê'},
        {'o', 'ô'}, {'O', 'Ô'}
    };

    // Reverse circumflex map
    private static readonly Dictionary<char, char> CircumflexReverse = new()
    {
        {'â', 'a'}, {'Â', 'A'},
        {'ê', 'e'}, {'Ê', 'E'},
        {'ô', 'o'}, {'Ô', 'O'}
    };

    // Vowel transformations with 'w' (breve/horn: ă, ơ, ư)
    private static readonly Dictionary<char, char> BreveHornMap = new()
    {
        {'a', 'ă'}, {'A', 'Ă'},
        {'o', 'ơ'}, {'O', 'Ơ'},
        {'u', 'ư'}, {'U', 'Ư'}
    };

    // Reverse breve/horn map
    private static readonly Dictionary<char, char> BreveHornReverse = new()
    {
        {'ă', 'a'}, {'Ă', 'A'},
        {'ơ', 'o'}, {'Ơ', 'O'},
        {'ư', 'u'}, {'Ư', 'U'}
    };

    // Base vowels for tone placement (includes modified vowels)
    private static readonly HashSet<char> AllVowels = new()
    {
        'a', 'ă', 'â', 'e', 'ê', 'i', 'o', 'ô', 'ơ', 'u', 'ư', 'y',
        'A', 'Ă', 'Â', 'E', 'Ê', 'I', 'O', 'Ô', 'Ơ', 'U', 'Ư', 'Y'
    };

    // Special vowels that get priority for tone placement
    private static readonly HashSet<char> SpecialVowels = new()
    {
        'ă', 'â', 'ê', 'ô', 'ơ', 'ư',
        'Ă', 'Â', 'Ê', 'Ô', 'Ơ', 'Ư'
    };

    // Consonants that can follow "ie" or "ye" to trigger auto-circumflex conversion
    private static readonly HashSet<char> IeYeFollowingConsonants = new()
    {
        'n', 'N', 'm', 'M', 't', 'T', 'c', 'C', 'p', 'P', 'u', 'U'
    };

    // Tone key to index: s=0 (sắc), f=1 (huyền), r=2 (hỏi), x=3 (ngã), j=4 (nặng)
    private static int GetToneIndex(char toneKey) => toneKey switch
    {
        's' => 0, 'f' => 1, 'r' => 2, 'x' => 3, 'j' => 4, _ => -1
    };

    // Compact tone mapping using Unicode strings (index: sắc, huyền, hỏi, ngã, nặng)
    private static readonly Dictionary<char, string> TonedVowels = new()
    {
        {'a', "áàảãạ"}, {'ă', "ắằẳẵặ"}, {'â', "ấầẩẫậ"},
        {'e', "éèẻẽẹ"}, {'ê', "ếềểễệ"}, {'i', "íìỉĩị"},
        {'o', "óòỏõọ"}, {'ô', "ốồổỗộ"}, {'ơ', "ớờởỡợ"},
        {'u', "úùủũụ"}, {'ư', "ứừửữự"}, {'y', "ýỳỷỹỵ"},
        {'A', "ÁÀẢÃẠ"}, {'Ă', "ẮẰẲẴẶ"}, {'Â', "ẤẦẨẪẬ"},
        {'E', "ÉÈẺẼẸ"}, {'Ê', "ẾỀỂỄỆ"}, {'I', "ÍÌỈĨỊ"},
        {'O', "ÓÒỎÕỌ"}, {'Ô', "ỐỒỔỖỘ"}, {'Ơ', "ỚỜỞỠỢ"},
        {'U', "ÚÙỦŨỤ"}, {'Ư', "ỨỪỬỮỰ"}, {'Y', "ÝỲỶỸỴ"}
    };

    // Get toned vowel: ApplyToneMark('a', 's') => 'á'
    private static char ApplyToneMark(char baseVowel, char toneKey)
    {
        int idx = GetToneIndex(toneKey);
        if (idx < 0 || !TonedVowels.TryGetValue(baseVowel, out var tones)) return baseVowel;
        return tones[idx];
    }

    // Get base vowel from toned vowel: GetBaseFromToned('á') => 'a'
    private static char GetBaseFromToned(char c)
    {
        foreach (var (baseV, tones) in TonedVowels)
            if (tones.Contains(c)) return baseV;
        return c;
    }

    // Get tone key from toned vowel: GetToneFromChar('á') => 's'
    private static char GetToneKeyFromChar(char c)
    {
        foreach (var (_, tones) in TonedVowels)
        {
            int idx = tones.IndexOf(c);
            if (idx >= 0) return "sfrxj"[idx];
        }
        return '\0';
    }

    // Extract base sym (ASCII only) for validation. SymatoSyms uses "dd" for "đ"
    private string GetBaseSym(string buffer) => string.Concat(buffer.Select(c =>
        char.ToLower(c) == 'đ' ? "dd" : char.ToLower(GetPureBaseVowel(c)).ToString()));

    private bool IsValidSym(string buffer) => SymatoSyms.Contains(GetBaseSym(buffer));

    private static bool HasToneMark(string buffer) => buffer.Any(c => GetToneKeyFromChar(c) != '\0');

    // If raw input has consecutive identical keystrokes, force raw output (no conversion).
    // Comparison is case-insensitive to match the physical key regardless of shift/caps.
    private static bool HasConsecutiveDuplicateKeystrokes(string raw)
    {
        if (raw.Length < 2) return false;

        for (int i = 1; i < raw.Length; i++)
        {
            if (char.ToLowerInvariant(raw[i]) == char.ToLowerInvariant(raw[i - 1]))
                return true;
        }

        return false;
    }

    // Tone Stop Rule: c/ch/t/p endings only allow sắc(s) or nặng(j)
    private static bool EndsWithStopConsonant(string b) =>
        b.Length >= 2 && (b.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
        "ctp".Contains(char.ToLower(b[^1])));

    private static bool IsValidToneForSyllable(string buffer, char toneKey) =>
        !EndsWithStopConsonant(buffer) || toneKey == 's' || toneKey == 'j';

    private static bool IsEndingConsonant(char c) => "nmtcpghNMTCPGH".Contains(c);

    private string RemoveToneMarks(string buffer) =>
        string.Concat(buffer.Select(c => GetBaseVowel(c)));

    // Render-time decision: return buffer if valid, else raw input
    private string GetRenderText()
    {
        if (_rawBuffer.Length == 0) return "";

        string raw = _rawBuffer.ToString();
        if (DoubleKeyRawEnabled && HasConsecutiveDuplicateKeystrokes(raw)) return raw;

        string buffer = _buffer.ToString();
        
        // Check basic syllable validity
        if (!IsValidSym(buffer)) return raw;
        
        // Check Tone Stop Rule: if buffer has a tone, validate it against ending consonant
        if (HasToneMark(buffer) && EndsWithStopConsonant(buffer))
        {
            // Find the tone in the buffer
            char toneKey = '\0';
            foreach (char c in buffer)
            {
                toneKey = GetToneKeyFromChar(c);
                if (toneKey != '\0') break;
            }
            
            // If tone violates Tone Stop Rule, fallback to raw
            if (toneKey != '\0' && !IsValidToneForSyllable(buffer, toneKey))
            {
                return raw;
            }
        }
        
        return buffer;
    }

    // Render to screen with minimal changes (diff-based)
    private void Render()
    {
        string newText = GetRenderText();
        if (newText == _renderedText) return;
        
        // Find common prefix
        int common = 0;
        int minLen = Math.Min(_renderedText.Length, newText.Length);
        while (common < minLen && _renderedText[common] == newText[common])
            common++;
        
        // Delete chars after common prefix
        int deleteCount = _renderedText.Length - common;
        if (deleteCount > 0)
            SendBackspaces(deleteCount);
        
        // Send new chars after common prefix
        if (common < newText.Length)
            SendString(newText.Substring(common));
        
        _renderedText = newText;
    }

    // Try to reposition existing tone mark to correct position after buffer changes
    private string? TryRepositionTone(string buffer)
    {
        // Early exit: No reposition needed if no tone or last char isn't ending consonant
        if (buffer.Length < 2) return null;
        if (!IsEndingConsonant(buffer[^1])) return null;
        if (!HasToneMark(buffer)) return null;
        if (!IsValidSym(buffer)) return null; // Don't reposition invalid syllables
        
        // Find existing tone
        char existingToneKey = '\0';
        int existingTonePos = -1;
        for (int i = 0; i < buffer.Length; i++)
        {
            char key = GetToneKeyFromChar(buffer[i]);
            if (key != '\0')
            {
                existingToneKey = key;
                existingTonePos = i;
                break;
            }
        }
        
        if (existingToneKey == '\0') return null;
        
        // Remove existing tone, get base buffer
        var baseBuffer = new StringBuilder(buffer);
        baseBuffer[existingTonePos] = GetBaseVowel(buffer[existingTonePos]);
        
        // Find new correct position
        int newPos = FindTonePosition(baseBuffer.ToString());
        if (newPos < 0 || newPos == existingTonePos) return null;
        
        // Apply tone at new position
        char vowelAtNewPos = baseBuffer[newPos];
        char tonedVowel = ApplyToneMark(vowelAtNewPos, existingToneKey);
        baseBuffer[newPos] = tonedVowel;
        
        return baseBuffer.ToString();
    }

    public bool ProcessKey(Keys key, ref bool handled)
    {
        // Ignore if Ctrl or Alt is pressed (shortcuts)
        if ((Control.ModifierKeys & (Keys.Control | Keys.Alt)) != 0)
        {
            ClearBuffers();
            return false;
        }
        
        // Ignore if Win key is pressed (Win+D, Win+E, etc.)
        // Control.ModifierKeys doesn't track Win key, so use GetAsyncKeyState
        const int VK_LWIN = 0x5B;
        const int VK_RWIN = 0x5C;
        if ((NativeMethods.GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 ||
            (NativeMethods.GetAsyncKeyState(VK_RWIN) & 0x8000) != 0)
        {
            ClearBuffers();
            return false;
        }

        // Check buffer timeout
        if ((DateTime.Now - _lastKeyTime).TotalMilliseconds > BufferTimeoutMs)
        {
            ClearBuffers();
        }
        _lastKeyTime = DateTime.Now;

        // Handle backspace - smart undo
        if (key == Keys.Back)
        {
            return HandleBackspace(ref handled);
        }

        // Handle ESC - revert to raw ASCII input
        if (key == Keys.Escape)
        {
            return HandleEscape(ref handled);
        }

        // Handle space/enter - commit and clear buffer
        if (key == Keys.Space || key == Keys.Enter)
        {
            ClearBuffers();
            return false;
        }

        // Handle navigation keys - clear buffer
        if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down ||
            key == Keys.Home || key == Keys.End || key == Keys.PageUp || key == Keys.PageDown)
        {
            ClearBuffers();
            return false;
        }

        // Get the character from the key
        char c = KeyToChar(key);
        if (c == '\0') 
        {
            ClearBuffers();
            return false;
        }

        char lowerC = char.ToLower(c);

        // Always track raw input
        _rawBuffer.Append(c);

        // Check for special keys: z, w, s, f, r, x, j, d
        if (lowerC == 'z' || lowerC == 'w' || lowerC == 's' || 
            lowerC == 'f' || lowerC == 'r' || lowerC == 'x' || 
            lowerC == 'j' || lowerC == 'd')
        {
            string? result = TryApplyModifier(_buffer.ToString(), lowerC);
            if (result != null)
            {
                _buffer.Clear();
                _buffer.Append(result);
                Render();
                handled = true;
                return true;
            }
        }

        // Check for ie/ye -> iê/yê auto-conversion
        if (IeYeFollowingConsonants.Contains(c))
        {
            string? result = TryAutoConvertIeYe(_buffer.ToString(), c);
            if (result != null)
            {
                _buffer.Clear();
                _buffer.Append(result);
                Render();
                handled = true;
                return true;
            }
        }

        // Regular character - add to buffer
        _buffer.Append(c);
        
        // Auto-reposition tone if needed (e.g., "muón" + "g" → "muống")
        string? repositioned = TryRepositionTone(_buffer.ToString());
        if (repositioned != null)
        {
            _buffer.Clear();
            _buffer.Append(repositioned);
        }
        
        // Render with validation (shows buffer if valid, raw if invalid)
        Render();
        handled = true;
        return true;
    }

    private bool HandleBackspace(ref bool handled)
    {
        if (_rawBuffer.Length == 0)
        {
            _buffer.Clear();
            _undoStack.Clear();
            return false;
        }
        
        // Remove last char from raw buffer
        _rawBuffer.Length--;
        
        // Rebuild buffer from raw input (like symato_droid)
        // Rebuild buffer from raw input
        RebuildFromRaw();
        
        // Render with diff-based updates
        Render();
        handled = true;
        return true;
    }
    
    // Rebuild _buffer from _rawBuffer by replaying all transformations
    // STRICT RULE: Modifiers only apply if ALL characters after them are also modifiers
    // This allows chaining (azs→ấ) but blocks non-modifier followers (asc→asc)
    private void RebuildFromRaw()
    {
        _buffer.Clear();
        _undoStack.Clear();
        
        string raw = _rawBuffer.ToString();
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            char lower = char.ToLower(c);

            // STRICT: Only apply modifier if ALL remaining chars are also modifiers
            // This allows: azs (z applies, then s applies) → ấ
            // This blocks: asc (s followed by non-modifier 'c') → asc
            if (IsModifierKey(lower) && AllRemainingAreModifiers(raw, i))
            {
                string? modified = TryApplyModifier(_buffer.ToString(), lower);
                if (modified != null)
                {
                    _buffer.Clear();
                    _buffer.Append(modified);
                    continue;
                }
            }
            
            // Try ie/ye auto-conversion before adding the character
            // This handles patterns like "tien" → "tiên" where 'n' triggers conversion
            if (IeYeFollowingConsonants.Contains(c) && !IsModifierKey(lower))
            {
                string? converted = TryAutoConvertIeYe(_buffer.ToString(), c);
                if (converted != null)
                {
                    _buffer.Clear();
                    _buffer.Append(converted);
                    continue;
                }
            }
            
            // Regular character - add to buffer
            _buffer.Append(c);
            
            // Try reposition tone after adding consonant
            string? repositioned = TryRepositionTone(_buffer.ToString());
            if (repositioned != null)
            {
                _buffer.Clear();
                _buffer.Append(repositioned);
            }
        }
    }
    
    // Helper: Check if char is a modifier key (mark or tone)
    private static bool IsModifierKey(char c) => "zwsfrxjd".Contains(c);
    
    // Helper: Check if all characters from position i to end are modifier keys
    private static bool AllRemainingAreModifiers(string raw, int i)
    {
        for (int j = i; j < raw.Length; j++)
        {
            if (!IsModifierKey(char.ToLower(raw[j])))
                return false;
        }
        return true;
    }

    private bool HandleEscape(ref bool handled)
    {
        if (_rawBuffer.Length > 0)
        {
            // Force show raw input by clearing buffer (Render will show raw)
            _buffer.Clear();
            _buffer.Append(_rawBuffer.ToString());
            SendBackspaces(_renderedText.Length);
            SendString(_rawBuffer.ToString());
            _renderedText = _rawBuffer.ToString();
            ClearBuffers();
            handled = true;
            return true;
        }
        ClearBuffers();
        return false;
    }

    private void ClearBuffers()
    {
        _buffer.Clear();
        _rawBuffer.Clear();
        _undoStack.Clear();
        _renderedText = "";
    }

    private string? TryApplyModifier(string buffer, char modifier)
    {
        if (string.IsNullOrEmpty(buffer)) return null;
        
        // Only apply modifiers to valid Vietnamese syllables
        if (!IsValidSym(buffer)) return null;

        // Handle 'd' -> 'đ' conversion
        if (modifier == 'd')
        {
            return TryConvertD(buffer);
        }

        // Handle 'z' for circumflex (â, ê, ô)
        if (modifier == 'z')
        {
            return TryApplyCircumflex(buffer);
        }

        // Handle 'w' for breve/horn (ă, ơ, ư)
        if (modifier == 'w')
        {
            return TryApplyBreveHorn(buffer);
        }

        // Handle tone marks (s, f, r, x, j)
        if (GetToneIndex(modifier) >= 0)
        {
            return TryApplyTone(buffer, modifier);
        }

        return null;
    }

    /// <summary>
    /// Auto-converts "ie" or "ye" to "iê" or "yê" when followed by a valid consonant.
    /// In Vietnamese orthography:
    /// - "iê" is used when there's a preceding consonant (tiên, kiến, điên)
    /// - "yê" is used when there's no preceding consonant (yên, yêu)
    /// Both require a following letter to use the circumflex form.
    /// </summary>
    private string? TryAutoConvertIeYe(string buffer, char followingChar)
    {
        // Skip if auto ie/ye conversion is disabled
        if (!AutoIeYeEnabled) return null;
        
        if (buffer.Length < 2) return null;

        // Get the last two characters
        char secondLast = buffer[buffer.Length - 2];
        char last = buffer[buffer.Length - 1];
        
        char secondLastLower = char.ToLower(secondLast);
        char lastLower = char.ToLower(last);
        char lastBase = GetBaseVowel(last);
        char lastBaseLower = char.ToLower(lastBase);

        // Check for "ie" or "ye" pattern where 'e' hasn't been converted yet
        bool isIePattern = (secondLastLower == 'i' || secondLastLower == 'y') && lastBaseLower == 'e';
        
        if (!isIePattern) return null;
        
        // Check if 'e' is already 'ê' (already has circumflex)
        if (lastBase == 'ê' || lastBase == 'Ê') return null;
        
        // Convert 'e' to 'ê' and append the following character
        char newE = char.IsUpper(last) ? 'Ê' : 'ê';
        
        // Transfer any existing tone from the original 'e' to 'ê'
        char result = TransferTone(last, newE);
        
        _undoStack.Push(new UndoAction(buffer.Length - 1, last, result, ActionType.Circumflex));
        
        return buffer.Substring(0, buffer.Length - 1) + result + followingChar;
    }

    private string? TryConvertD(string buffer)
    {
        if (buffer.Length == 0) return null;
        
        char first = buffer[0];
        char firstLower = char.ToLower(first);
        
        // Toggle: d → đ
        if (firstLower == 'd')
        {
            char oldChar = first;
            char replacement = char.IsUpper(first) ? 'Đ' : 'đ';
            _undoStack.Push(new UndoAction(0, oldChar, replacement, ActionType.DToD));
            return replacement + buffer.Substring(1);
        }
        
        // Toggle: đ → d (revert)
        if (firstLower == 'đ')
        {
            char oldChar = first;
            char replacement = char.IsUpper(first) ? 'D' : 'd';
            _undoStack.Push(new UndoAction(0, oldChar, replacement, ActionType.DToD));
            return replacement + buffer.Substring(1);
        }
        
        return null;
    }

    private string? TryApplyCircumflex(string buffer)
    {
        // Find vowels from LEFT to RIGHT that can take circumflex (a, e, o)
        for (int i = 0; i < buffer.Length; i++)
        {
            char c = buffer[i];
            char baseC = GetPureBaseVowel(c);
            
            // Skip if already has circumflex
            char baseVowel = GetBaseVowel(c);
            bool alreadyHasCircumflex = CircumflexReverse.ContainsKey(baseVowel);
            
            if (!alreadyHasCircumflex && CircumflexMap.TryGetValue(baseC, out char modified))
            {
                // Transfer any existing tone
                char result = TransferTone(c, modified);
                _undoStack.Push(new UndoAction(i, c, result, ActionType.Circumflex));
                return buffer.Substring(0, i) + result + buffer.Substring(i + 1);
            }
        }
        return null;
    }

    private string? TryApplyBreveHorn(string buffer)
    {
        // Special case: "uo" -> "ươ" (e.g., muon -> mươn, luon -> lươn)
        // In Vietnamese, the pattern is "uo" not "ou" - u comes before o
        // Find "uo" pattern where neither already has breve/horn
        for (int i = 0; i < buffer.Length - 1; i++)
        {
            char c1 = buffer[i];
            char c2 = buffer[i + 1];
            char base1 = GetPureBaseVowel(c1);
            char base2 = GetPureBaseVowel(c2);

            // Check for "uo" pattern (case insensitive) - u followed by o
            if (char.ToLower(base1) == 'u' && char.ToLower(base2) == 'o')
            {
                char baseVowel1 = GetBaseVowel(c1);
                char baseVowel2 = GetBaseVowel(c2);
                bool has1 = BreveHornReverse.ContainsKey(baseVowel1);
                bool has2 = BreveHornReverse.ContainsKey(baseVowel2);

                // If neither has breve/horn yet, convert both
                if (!has1 && !has2)
                {
                    // Convert u -> ư
                    char newU = char.IsUpper(base1) ? 'Ư' : 'ư';
                    char result1 = TransferTone(c1, newU);
                    
                    // Convert o -> ơ
                    char newO = char.IsUpper(base2) ? 'Ơ' : 'ơ';
                    char result2 = TransferTone(c2, newO);
                    
                    _undoStack.Push(new UndoAction(i, c1, result1, ActionType.BreveHorn));
                    // Note: We only push one undo action for simplicity
                    
                    return buffer.Substring(0, i) + result1 + result2 + buffer.Substring(i + 2);
                }
            }
        }
        
        // Special case: "oa" -> "oă" (e.g., hoac -> hoăc, toac -> toăc)
        // In Vietnamese, "oă" combinations are valid (hoặc, toặc), but "ơa" is not
        for (int i = 0; i < buffer.Length - 1; i++)
        {
            char c1 = buffer[i];
            char c2 = buffer[i + 1];
            char base1 = GetPureBaseVowel(c1);
            char base2 = GetPureBaseVowel(c2);

            // Check for "oa" pattern (case insensitive) - o followed by a
            if (char.ToLower(base1) == 'o' && char.ToLower(base2) == 'a')
            {
                char baseVowel2 = GetBaseVowel(c2);
                bool has2 = BreveHornReverse.ContainsKey(baseVowel2);

                // If 'a' doesn't have breve yet, convert a -> ă
                if (!has2)
                {
                    char newA = char.IsUpper(base2) ? 'Ă' : 'ă';
                    char result = TransferTone(c2, newA);

                    _undoStack.Push(new UndoAction(i + 1, c2, result, ActionType.BreveHorn));

                    return buffer.Substring(0, i + 1) + result + buffer.Substring(i + 2);
                }
            }
        }
        
        // Special case: "ua" + consonant -> "uă" (e.g., quang -> quăng, tuan -> tuăn)
        // ONLY when there's a consonant AFTER the "ua" pattern
        // If "ua" is at end of syllable (cua), let default behavior handle it (u -> ư)
        // This is because: "quăng" is valid, "qưang" is not; but "cưa" is valid, "cuă" is not
        for (int i = 0; i < buffer.Length - 1; i++)
        {
            char c1 = buffer[i];
            char c2 = buffer[i + 1];
            char base1 = GetPureBaseVowel(c1);
            char base2 = GetPureBaseVowel(c2);

            // Check for "ua" pattern (case insensitive) - u followed by a
            if (char.ToLower(base1) == 'u' && char.ToLower(base2) == 'a')
            {
                // IMPORTANT: Only apply breve to 'a' if there's a consonant after
                // "quang" + w → quăng (consonant 'ng' after 'ua')
                // "cua" + w → cưa (no consonant after, use default behavior for u → ư)
                bool hasConsonantAfter = i + 2 < buffer.Length && !IsVowel(buffer[i + 2]);
                
                if (hasConsonantAfter)
                {
                    char baseVowel2 = GetBaseVowel(c2);
                    bool has2 = BreveHornReverse.ContainsKey(baseVowel2);

                    // If 'a' doesn't have breve yet, convert a -> ă
                    if (!has2)
                    {
                        char newA = char.IsUpper(base2) ? 'Ă' : 'ă';
                        char result = TransferTone(c2, newA);

                        _undoStack.Push(new UndoAction(i + 1, c2, result, ActionType.BreveHorn));

                        return buffer.Substring(0, i + 1) + result + buffer.Substring(i + 2);
                    }
                }
            }
        }

        // Default behavior: Find first vowel that can take breve/horn
        for (int i = 0; i < buffer.Length; i++)
        {
            char c = buffer[i];
            char baseC = GetPureBaseVowel(c);
            
            // Check if this vowel already has breve/horn
            char baseVowel = GetBaseVowel(c);
            bool alreadyHasBreveHorn = BreveHornReverse.ContainsKey(baseVowel);
            
            if (!alreadyHasBreveHorn && BreveHornMap.TryGetValue(baseC, out char modified))
            {
                // Transfer any existing tone
                char result = TransferTone(c, modified);
                _undoStack.Push(new UndoAction(i, c, result, ActionType.BreveHorn));
                return buffer.Substring(0, i) + result + buffer.Substring(i + 1);
            }
        }
        return null;
    }

    private string? TryApplyTone(string buffer, char toneKey)
    {
        // Only apply tone to valid Vietnamese syllables
        if (!IsValidSym(buffer)) return null;
        
        // Tone Stop Rule: c/ch/t/p endings only allow sắc (s) or nặng (j)
        if (!IsValidToneForSyllable(buffer, toneKey)) return null;
        
        int tonePos = FindTonePosition(buffer);
        if (tonePos < 0) return null;

        char vowel = buffer[tonePos];
        char baseVowel = GetBaseVowel(vowel);
        char toned = ApplyToneMark(baseVowel, toneKey);
        
        if (toned != baseVowel)
        {
            _undoStack.Push(new UndoAction(tonePos, vowel, toned, ActionType.Tone));
            return buffer.Substring(0, tonePos) + toned + buffer.Substring(tonePos + 1);
        }

        return null;
    }

    private int FindTonePosition(string buffer)
    {
        // ==========================================================
        // Vietnamese Tone Placement Rules (Quy tắc bỏ dấu tiếng Việt)
        // ==========================================================
        // Rule 1: Special vowels (ă, â, ê, ô, ơ, ư) ALWAYS get the tone - highest priority
        // Rule 2: 'u' after 'q' is a semivowel, NOT a vowel (qu = consonant cluster)
        //         Example: quá (tone on 'a'), NOT qúa
        // Rule 3: 'i' after 'g' followed by another vowel is semivowel (gi = consonant)
        //         Example: giá (tone on 'a'), NOT gía
        // Rule 4: For oa, oe, uy patterns → tone on the SECOND vowel
        //         Example: hoà, khoẻ, thuỷ
        // Rule 5: With ending consonant → tone on LAST vowel of the vowel cluster
        //         Example: tiếng (tone on 'ê'), oán (tone on 'a')
        // Rule 6: Without ending consonant → tone on PENULTIMATE vowel
        //         Example: kiểu (tone on 'ê'), hòa (tone on 'a')
        // ==========================================================
        
        // Get vowel positions, excluding semivowels
        var vowelPositions = new List<int>();
        
        for (int i = 0; i < buffer.Length; i++)
        {
            if (!IsVowel(buffer[i])) continue;
            
            // Rule 2: Skip 'u' after 'q' (qu is a consonant cluster)
            // qua, quy, quá, quý - 'u' is NOT a vowel here
            if (i > 0 && char.ToLower(buffer[i]) == 'u' && char.ToLower(buffer[i-1]) == 'q')
                continue;
            
            // Rule 3: Skip 'i' in 'gi' when followed by another vowel (gi is a consonant cluster)
            // gia, giá, già - first 'i' is NOT a vowel here
            if (i > 0 && i < buffer.Length - 1 && 
                char.ToLower(buffer[i]) == 'i' && char.ToLower(buffer[i-1]) == 'g' &&
                IsVowel(buffer[i+1]))
                continue;
            
            vowelPositions.Add(i);
        }

        if (vowelPositions.Count == 0) return -1;
        if (vowelPositions.Count == 1) return vowelPositions[0];

        // Get the last two vowels for pattern matching (needed for diphthong detection)
        int lastVowelPos = vowelPositions[^1];
        int secondLastVowelPos = vowelPositions.Count >= 2 ? vowelPositions[^2] : -1;
        
        // Check if ends with consonant (any character after the last vowel)
        bool endsWithConsonant = lastVowelPos < buffer.Length - 1;

        // Rule 1: Special vowels (ă, â, ê, ô, ơ, ư) get the tone
        // For diphthongs like ươ, iê, uô with ending consonant → tone on SECOND vowel (ơ, ê, ô)
        // Example: mướn (tone on ơ), tiếng (tone on ê), chuống (tone on ô)
        // For diphthongs without ending consonant → tone on FIRST vowel (ư, i, u)
        // Example: mưa (tone on ư), kia (no special vowel case)
        
        // Find all special vowel positions
        var specialVowelPositions = new List<int>();
        foreach (int pos in vowelPositions)
        {
            char baseV = GetBaseVowel(buffer[pos]);
            if (SpecialVowels.Contains(baseV))
            {
                specialVowelPositions.Add(pos);
            }
        }
        
        if (specialVowelPositions.Count > 0)
        {
            // Check for diphthong patterns: ươ, iê, uô (two consecutive special vowels)
            // For these diphthongs, tone ALWAYS goes on the SECOND special vowel
            // Examples: người (ơ), mười (ơ), tiếu (ê), muối (ô)
            if (specialVowelPositions.Count >= 2)
            {
                // Check if the two special vowels are consecutive (forming a diphthong)
                int first = specialVowelPositions[0];
                int second = specialVowelPositions[1];
                if (second == first + 1)
                {
                    // Consecutive special vowels = diphthong → tone on SECOND vowel
                    return second;
                }
            }
            
            // For single special vowel, return its position
            return specialVowelPositions[0];
        }

        // Get base vowels for pattern matching
        char lastBase = char.ToLower(GetBaseVowel(buffer[lastVowelPos]));
        char secondLastBase = secondLastVowelPos >= 0 ? char.ToLower(GetBaseVowel(buffer[secondLastVowelPos])) : '\0';

        // Rule 4: Special vowel combinations - oa, oe, uy → tone on SECOND vowel
        // Example: hoà (not hòa), khoẻ (not khòe), thuỷ (not thùy)
        if (vowelPositions.Count >= 2)
        {
            // oa pattern: hoà, toà, xoá
            if (secondLastBase == 'o' && lastBase == 'a')
            {
                return lastVowelPos; // tone on 'a'
            }
            // oe pattern: khoẻ, xoè, hoè
            if (secondLastBase == 'o' && lastBase == 'e')
            {
                return lastVowelPos; // tone on 'e'
            }
            // uy pattern: thuỷ, quỷ, nguỵ
            if (secondLastBase == 'u' && lastBase == 'y')
            {
                return lastVowelPos; // tone on 'y'
            }
        }

        // Rule 5 & 6: Position based on ending consonant
        if (endsWithConsonant)
        {
            // Rule 5: With ending consonant → tone on LAST vowel
            // Example: tiếng (tone on last vowel 'e' → 'ế'), oán (tone on 'á')
            return lastVowelPos;
        }
        else
        {
            // Rule 6: Without ending consonant → tone on PENULTIMATE vowel
            // Example: kiểu (tone on 'ê'), mùa (tone on 'u')
            if (vowelPositions.Count >= 2)
            {
                return secondLastVowelPos;
            }
            return lastVowelPos;
        }
    }

    private bool IsVowel(char c)
    {
        char baseC = GetBaseVowel(c);
        return AllVowels.Contains(baseC);
    }

    private char GetBaseVowel(char c) => GetBaseFromToned(c);

    private char GetPureBaseVowel(char c)
    {
        // Remove both tone and diacritic (â->a, ă->a, ê->e, etc.)
        char baseV = GetBaseVowel(c);
        
        if (CircumflexReverse.TryGetValue(baseV, out char pure1))
            return pure1;
        if (BreveHornReverse.TryGetValue(baseV, out char pure2))
            return pure2;
        
        return baseV;
    }

    private char TransferTone(char original, char newBase)
    {
        char tone = GetToneKey(original);
        if (tone != '\0') return ApplyToneMark(newBase, tone);
        return newBase;
    }

    private char GetToneKey(char c) => GetToneKeyFromChar(c);

    private void SendBackspaces(int count)
    {
        for (int i = 0; i < count; i++)
        {
            NativeMethods.SendKey(Keys.Back);
        }
    }

    private void SendString(string s) => NativeMethods.SendString(s);

    private char KeyToChar(Keys key)
    {
        bool shift = (Control.ModifierKeys & Keys.Shift) != 0;
        bool capsLock = Control.IsKeyLocked(Keys.CapsLock);
        bool upper = shift ^ capsLock;

        if (key >= Keys.A && key <= Keys.Z)
        {
            char c = (char)('a' + (key - Keys.A));
            return upper ? char.ToUpper(c) : c;
        }

        return '\0';
    }

    public void Reset()
    {
        ClearBuffers();
    }

    #region Test Helpers (for unit testing without Windows API)
    
    public string TestGetBaseSym(string buffer) => GetBaseSym(buffer);
    public bool TestIsValidSym(string buffer) => IsValidSym(buffer);
    public static char TestApplyToneMark(char baseVowel, char toneKey) => ApplyToneMark(baseVowel, toneKey);
    public static char TestGetBaseFromToned(char c) => GetBaseFromToned(c);
    
    /// <summary>
    /// Simulate typing a string and return the render result (without sending to screen)
    /// Uses RebuildFromRaw() to ensure strict end-of-syllable modifier rule
    /// </summary>
    public string SimulateTyping(string input)
    {
        ClearBuffers();
        
        foreach (char c in input)
        {
            _rawBuffer.Append(c);
            
            // Rebuild buffer from raw - this handles:
            // 1. STRICT end-of-syllable modifier rule
            // 2. ie/ye auto-conversion
            // 3. Tone repositioning
            RebuildFromRaw();
        }
        
        return GetRenderText();
    }
    
    #endregion
}
