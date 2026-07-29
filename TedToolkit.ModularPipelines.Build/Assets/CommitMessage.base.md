Always output in this format:
<gitmojis> <type>(<scope>): <subject>

<body>

[optional footer]

Rules:

1. Gitmojis: use 1–2 total, matching the change type.
    - **CRITICAL: Use the emojis from this official list for reference:** https://raw.githubusercontent.com/carloscuesta/gitmoji/refs/heads/master/packages/gitmojis/src/gitmojis.json
    - **CRITICAL: Output the actual emoji character (e.g., '🎨', '✨'), NOT the entity code (e.g., '&#x1f3a8;') or the shortcode (e.g., ':art:').**

2. type: one of feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert.

3. scope: optional short noun (e.g., ui, api).

4. subject: imperative mood, concise, no period.

5. body:
    - Provide clear context: explain **what** and **why** the change is made, avoiding "how".
    - **CRITICAL: The body MUST start with 1–2 concise paragraphs summarizing the *entire* change and its purpose.**
    - **Immediately following the summary paragraphs,** use bullet points for multiple specific changes, details, or important notes.
    - Make it informative enough for someone reviewing the commit history without reading the code.
    - Avoid vague or overly terse descriptions.
    - Keep the message as short as possible.

6. Breaking changes:
    - Automatically detect if the change is breaking from the description.
    - Append `!` after type or scope if this commit introduces a breaking change.
    - Include a footer in this format:

      BREAKING CHANGE: <clear explanation of what was changed and its impact>!

    - Use imperative / clear language in the footer.
    - A BREAKING CHANGE can be part of commits of any type.
    - **CRITICAL: The body MUST start with 1–2 concise paragraphs summarizing the *entire* change and its purpose.**

**CRITICAL: The final output MUST be the plain text commit message, strictly adhering to the specified format without any surrounding tags like <gitmojis>, <body>, [optional footer], or similar XML/tag-like elements.**