# AGENTS.md - SymatoIME Development Guidelines

## Engine Rules

### Modifier Keys (Strict End-of-Syllable Rule)

All modifiers are only applied when they appear at the END of a syllable (or followed only by other modifiers).

#### Marks (Diacritics)
Keys that add diacritics to vowels:
- **`d`** - Stroke: converts `d` → `đ` (at syllable start)
- **`z`** - Circumflex: `a` → `â`, `e` → `ê`, `o` → `ô`
- **`w`** - Breve/Horn: `a` → `ă`, `o` → `ơ`, `u` → `ư`

#### Tones
Keys that add tone marks to the appropriate vowel:
- **`s`** - Sắc (acute): `a` → `á`
- **`f`** - Huyền (grave): `a` → `à`
- **`r`** - Hỏi (hook above): `a` → `ả`
- **`x`** - Ngã (tilde): `a` → `ã`
- **`j`** - Nặng (dot below): `a` → `ạ`

### Strict Rule Definition

**Modifiers only apply if ALL remaining characters after them are also modifiers.**

This rule:
- ✅ **Allows chaining**: `azs` → `ấ` (z applies circumflex, s applies tone)
- ✅ **Allows end placement**: `muons` → `muón` (s is last, applies tone)
- ❌ **Blocks mid-syllable modifiers**: `asc` → `asc` (s followed by non-modifier 'c')
- ❌ **Blocks mid-word d**: `dang` → `dang` (d followed by non-modifiers)

### Examples

| Input | Output | Explanation |
|-------|--------|-------------|
| `as` | `á` | s at end → tone applied |
| `asc` | `asc` | s NOT at end → no conversion |
| `azs` | `ấ` | z+s both modifiers → both apply |
| `dangd` | `đang` | d at end → toggles first d to đ |
| `dang` | `dang` | d NOT at end → no đ conversion |
| `hoacw` | `hoăc` | w at end → breve on 'a' |
| `hoawc` | `hoawc` | w NOT at end → no conversion |
| `muonws` | `mướn` | w+s both modifiers → ươ + sắc |

This is enforced in `RebuildFromRaw()` which is called after every keystroke.
