---
name: commit-message
description: Generate a one-line conventional commit message based on staged git changes and copy it to the clipboard. Use when the user wants a commit message generated from staged changes, or says "/commit-message".
---

# Commit Message Generator

Generate a concise, one-line commit message from staged git changes, then copy it to the clipboard.

## Steps

1. Run `git diff --staged` to get the staged changes.
2. Also run `git log --oneline -5` to understand the existing commit style in the repo.
3. Analyze the diff and produce a **single-line** commit message following the Conventional Commits format:
   - Format: `<type>(<optional scope>): <short imperative description>`
   - Types: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`, `style`, `perf`, `ci`
   - Keep it under 72 characters
   - Use imperative mood ("add", "fix", "update" — not "added", "fixed", "updated")
   - No trailing period
4. Copy the message to the clipboard using `echo -n "<message>" | pbcopy` (macOS) or `echo -n "<message>" | xclip -selection clipboard` (Linux).
5. Output the commit message to the user so they can see what was copied.

## Rules

- Output **only one line** as the commit message — no body, no footer.
- If there are no staged changes, tell the user and stop.
- Do not create a commit — only generate and copy the message.
- Do not ask for confirmation; generate and copy immediately.
