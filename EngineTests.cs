namespace SymatoIME;

/// <summary>
/// Engine tests inspired by symato_droid test runner
/// Run with: dotnet run -c Test -- --test
/// </summary>
public static class EngineTests
{
    private static int _failed = 0;

    public static void RunAll()
    {
        Console.WriteLine("=== SymatoIME Engine Tests ===\n");
        
        // === TONE TESTS ===
        Check("as", "á", "tone_s");
        Check("af", "à", "tone_f");
        Check("ar", "ả", "tone_r");
        Check("ax", "ã", "tone_x");
        Check("aj", "ạ", "tone_j");
        
        // === CIRCUMFLEX TESTS (z) ===
        Check("az", "â", "circumflex_a");
        Check("ez", "ê", "circumflex_e");
        Check("oz", "ô", "circumflex_o");
        
        // === HORN/BREVE TESTS (w) ===
        Check("aw", "ă", "breve_a");
        Check("ow", "ơ", "horn_o");
        Check("uw", "ư", "horn_u");
        
        // === COMBINED TONE + DIACRITIC ===
        Check("azs", "ấ", "tone_on_circumflex");
        Check("uwj", "ự", "tone_on_horn");
        
        // === HORN ON UO CLUSTER ===
        Check("tuongw", "tương", "horn_uo_cluster");
        Check("muonws", "mướn", "horn_uo_tone");
        Check("luonw", "lươn", "luon_horn");
        
        // === VALID VIETNAMESE SYLLABLES ===
        Check("quas", "quá", "valid_qu");
        Check("gias", "giá", "valid_gi");
        Check("muons", "muón", "valid_muon");
        
        // === AUTO IE/YE -> IÊ/YÊ CONVERSION ===
        Check("tien", "tiên", "auto_ie_conversion");
        Check("yen", "yên", "auto_ye_conversion");
        Check("tiens", "tiến", "auto_ie_with_tone");  // n triggers ie→iê, s is modifier at end
        Check("nguyen", "nguyên", "auto_uye_conversion");  // UYE pattern
        
        // === INVALID SYLLABLES -> RAW OUTPUT ===
        Check("rerun", "rerun", "invalid_rerun_raw");
        Check("xyz", "xyz", "invalid_xyz_raw");
        Check("zzs", "zzs", "invalid_no_transform");
        Check("aks", "aks", "invalid_final_k");
        
        // === FROM SYMATO_DROID ===
        Check("tuons", "tuón", "valid_closed_uo");
        Check("tias", "tía", "valid_open_ia");
        Check("ties", "ties", "invalid_open_ie");  // "tie" without closing consonant is invalid
        
        // === TONE STOP RULE (c/ch/t/p only allow sắc/nặng) ===
        Check("hocs", "hóc", "tone_stop_s_valid");
        Check("hocj", "học", "tone_stop_j_valid");
        Check("hocf", "hocf", "tone_stop_f_invalid");  // huyền not allowed
        Check("hocr", "hocr", "tone_stop_r_invalid");  // hỏi not allowed
        Check("hocx", "hocx", "tone_stop_x_invalid");  // ngã not allowed
        Check("sachs", "sách", "tone_stop_ch_s_valid");
        Check("sachf", "sachf", "tone_stop_ch_f_invalid");
        Check("mats", "mát", "tone_stop_t_s_valid");
        Check("matf", "matf", "tone_stop_t_f_invalid");
        Check("deps", "dép", "tone_stop_p_s_valid");  // d + e + p + s = dép
        Check("depf", "depf", "tone_stop_p_f_invalid");
        
        // === TONE STOP RULE - LATE CONSONANT (render-time validation) ===
        Check("next", "next", "tone_stop_late_t_x_invalid");  // 'x' applied before 't', still invalid
        Check("nect", "nect", "tone_stop_late_t_other");       // similar pattern
        Check("nexc", "nexc", "tone_stop_late_c_x_invalid");   // 'x' invalid with 'c' ending
        
        // === OA + W PATTERN (oa -> oă, not ơa) - STRICT: mark at end ===
        Check("hoacw", "hoăc", "oa_breve_hoac");     // STRICT: w at end
        Check("toacw", "toăc", "oa_breve_toac");     // STRICT: w at end
        Check("loangw", "loăng", "oa_breve_loang");  // STRICT: w at end
        Check("hoacwj", "hoặc", "oa_breve_with_tone"); // STRICT: mark+tone at end
        
        // === DIPHTHONG TONE PLACEMENT (ươ, iê, uô - tone on SECOND vowel) ===
        Check("nguoiwf", "người", "diphthong_uoi_tone_on_o");     // ươi + f → tone on ơ
        Check("muoiws", "mưới", "diphthong_uoi_tone_sac");         // ươi + s → sắc on ơ
        Check("tuoiw", "tươi", "diphthong_uoi_no_tone");           // ươi without tone
        Check("muonwf", "mườn", "diphthong_uon_tone_on_o");        // ươn + f → tone on ơ
        
        // === FROM SYMATO_DROID: DOUBLE-KEY REVERT ===
        Check("aww", "aww", "double_w_revert");     // double 'w' → revert to raw
        Check("azz", "azz", "double_z_revert");     // double 'z' → revert to raw
        // Note: "ass" → "á" in symato_qoder (tone applied once, 's' ignored second time)
        // This differs from symato_droid where "ass" → "ass" (double-key revert)
        
        // === FROM SYMATO_DROID: STROKE TOGGLE ===
        Check("dad", "đa", "stroke_d");             // d + a + d → đa
        Check("dadd", "da", "stroke_toggle");       // toggle đ back to d
        
        // === FROM SYMATO_DROID: UA CLUSTER ===
        Check("cuaw", "cưa", "horn_ua_cluster");    // ua + w → ưa
        
        // === STRICT END-OF-SYLLABLE RULE ===
        // Modifiers (d,z,w,s,f,r,x,j) are ONLY applied when they are the LAST char
        Check("asc", "asc", "strict_tone_not_at_end");      // 's' not at end → no tone
        Check("azc", "azc", "strict_mark_not_at_end");      // 'z' not at end → no circumflex  
        Check("dac", "dac", "strict_d_not_at_end");         // 'd' not at end → no đ
        Check("awc", "awc", "strict_w_not_at_end");         // 'w' not at end → no breve
        Check("muosnc", "muosnc", "strict_tone_mid");       // 's' in middle → no conversion
        Check("toawng", "toawng", "strict_w_mid_cluster");  // 'w' in middle → no breve (compare to "toawngw")
        Check("dang", "dang", "strict_d_start_only");       // 'd' start, ends with 'g' → no đ
        Check("dangd", "đang", "strict_d_at_end_toggle");   // 'd' at end → applies đ
        
        // Print results
        int total = _failed + CountPassed();
        Console.WriteLine($"\n=== Results: {total - _failed} passed, {_failed} failed ===");
        
        if (_failed == 0)
            Console.WriteLine("{\"ok\":true,\"failed\":0}");
        else
            Console.WriteLine($"{{\"ok\":false,\"failed\":{_failed}}}");
        
        Environment.Exit(_failed > 0 ? 1 : 0);
    }

    private static int _passed = 0;
    private static int CountPassed() => _passed;

    private static void Check(string input, string expected, string name)
    {
        var converter = new VietnameseConverter();
        string actual = converter.SimulateTyping(input);
        
        if (actual == expected)
        {
            Console.WriteLine($"  ✓ {name}: '{input}' -> '{actual}'");
            _passed++;
        }
        else
        {
            Console.WriteLine($"  ✗ {name}: '{input}' -> '{actual}', expected '{expected}'");
            _failed++;
        }
    }
}
