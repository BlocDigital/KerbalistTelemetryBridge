---
name: kerbalist-changes-summary
description: Detects changes in Kerbalist src/parts/ and summarizes them.
version: 1.0.0
author: digit
license: MIT
platforms: [windows, linux, macos]
metadata:
  category: software-development
  related_skills: [systematic-debugging, test-driven-development, requesting-code-review]
  usage_command: /kerbalist-changes-summary
---

# kerbalist-changes-summary

Detects local changes to Kerbalist's `src/parts/` directory and generates a concise summary ready to paste into Claude.

## Use When

- You've edited several `.py` part files and want to give Claude a focused summary
- You want to know what changed before asking for a review
- You need to decide whether to commit or discard changes

## How It Works

1. Runs `git diff --name-only HEAD` to find modified files in `src/parts/`
2. Runs `git ls-files --others --exclude-standard` to find untracked files
3. For each changed file: shows size change, first ~500 chars of diff
4. Produces a structured summary with timestamps

## Usage

```bash
# From the Kerbalist repo directory:
python kerbalist-changes-summary.py

# Or specify a different repo path:
python kerbalist-changes-summary.py --repo-path /path/to/kerbalist

# Copy summary to clipboard:
python kerbalist-changes-summary.py --copy

# JSON output for programmatic use:
python kerbalist-changes-summary.py --json
```

## Slash Command Usage

In the Hermes desktop app or CLI, use:

```
/kerbalist-changes-summary
```

This will execute the skill and display the diff summary in the chat.

## Example Output

```
======================================================================
KERBALIST LOCAL CHANGES - 2026-10-10 14:32:15
======================================================================

📝 MODIFIED (2 files):

  • src/parts/Kerbalist/Parts/PartsLibrary/PartsLibraryView.py
    4234→4512 bytes (+278)
    Diff:
      + def filter_by_category(self, category: str) -> List[str]:
      +     return [p for p in self.parts if category in p.name]

✨ NEW (1 file):

  • src/parts/Kerbalist/Parts/PartsLibrary/CustomPartEditor.py (8421 bytes)

======================================================================
Copy this block and paste into Claude with: 'Here are my local changes:'
======================================================================
```

## Limitations

- Requires `git` to be installed and available on PATH
- Diff output is truncated to ~500 chars per file to keep summaries concise
- Does not analyze semantic changes — only structural/file-level changes
- Requires the `src/parts/` directory to exist under git tracking
- Requires Python 3.10+
- Optional: `pyperclip` for clipboard copy (`pip install pyperclip`)

## Notes

- This skill is intended for use with the Kerbalist repository
- The summary is designed to be pasted directly into Claude for review
- The script is standalone and can be run independently of Hermes
- Works best when run from the root of the Kerbalist repository

## Source Code

```python
[The full Python script is included in the skill's attached file:]
D:/AI-PROJECTS/KerbalistTelemetryBridge/skills/kerbalist-changes-summary/kerbalist-changes-summary.py
```

## Reference Files

- `kerbalist-changes-summary.py` — the executable Python script (attached)
- `README.md` — additional usage notes (optional)