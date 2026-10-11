# kerbalist-changes-summary — Global Skill Placement Guide

## Where to Put Skills

Place a skill as a **subdirectory** under the global skills directory:

```
~/.hermes/skills/<skill-name>/
├── SKILL.md        # skill definition (required)
└── <skill>.py      # executable script (required)
```

### Global Skills Directory

```bash
# Windows
$HOME/.hermes/skills/<skill-name>

# Linux / macOS
$HOME/.hermes/skills/<skill-name>
```

### Directory Structure

```
~/.hermes/skills/
└── <skill-name>/          # ← skill goes in a SUBDIRECTORY
    ├── SKILL.md          # skill metadata and documentation
    └── <skill>.py        # executable Python script
```

### Critical Pitfall — The Stray File

**NEVER** place a skill file directly in `~/.hermes/skills/` as a loose file.

```
❌ WRONG: ~/.hermes/skills/
   ├── kerbalist-changes-summary.py   ← stray file (ignored)
   ├── SKILL.md                        ← stray file (ignored)
   └── <skill-name>/                   ← this is where skills belong
```

```
✅ CORRECT: ~/.hermes/skills/
   └── <skill-name>/                    ← skill in subdirectory
       ├── SKILL.md
       └── <skill>.py
```

A stray file at the top level of the skills directory will **hide** your skill from Hermes' discovery process. Hermes scans subdirectories, not loose files.

## Usage

### Manual Placement

```bash
# Create the directory
mkdir -p ~/.hermes/skills/<skill-name>

# Copy your skill files there
cp your-skill.py ~/.hermes/skills/<skill-name>/
cp SKILL.md ~/.hermes/skills/<skill-name>/

# Verify the directory structure
ls -la ~/.hermes/skills/<skill-name>/
```

### Using skill_manage

When creating a skill with `skill_manage`, always specify:

- `category`: a category name (e.g., `software-development`, `hermes`, `local-telemetry-bridge-api`)
- `file_path`: relative path within the skill directory (e.g., `SKILL.md`)

### Verifying a Skill is Registered

```bash
hermes skill list
```

Or call the skill directly:

```
/kerbalist-changes-summary
```

## How Hermes Discovers Skills

On startup, Hermes:

1. Scans `~/.hermes/skills/` for **subdirectories** (not files).
2. For each subdirectory, looks for:
   - `SKILL.md` — contains the skill's metadata (name, description, command prefix).
   - `<name>.py` — the executable script.
3. Registers the skill under its name in the slash-command registry.

## Troubleshooting

### Skill not found

```
Error: Unknown skill(s): my-skill
```

**Check:**

1. The skill directory exists: `~/.hermes/skills/<skill-name>/`
2. `SKILL.md` exists inside it.
3. No stray files exist at `~/.hermes/skills/` (top level).
4. Restart the Hermes desktop app (it caches the skill registry on startup).

### Skill returns 500 error

The skill is registered but the HTTP handler is throwing an exception. Read the skill's Python file and check for:

- Missing imports
- Missing dependencies (`git`, `pyperclip`, etc.)
- Logic that assumes a certain filesystem state (e.g., `src/parts/` exists)

## Reference Files

- `references/skill-directory-structure.md` — detailed directory layout examples
- `references/skill-manage-options.md` — `skill_manage` parameter reference

---

## Source Code

See the attached skill file for the full implementation.

## See Also

- [Hermes Agent Documentation](https://hermes-agent.nousresearch.com/docs)
- `hermes-agent` skill — for configuring Hermes settings
- `hermes-agent` bundled skill — for advanced Hermes configuration
