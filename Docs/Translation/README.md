# Dhivehi translation

- `Dhivehi-Translation.docx` — the workbook sent to the owner (1,147 lines, grouped by
  screen, plus a glossary and questions about fonts and language settings). The owner
  fills in the yellow "Dhivehi" column and returns it with the font files.
- `dhivehi-keys.json` — every line's key (e.g. `VOTER-012`), English text and where it
  appears. Keys map the returned translations onto the app's resource files.
- `tools/extract.py` — collects the English text from the views, web controllers and
  models, and the server's user-facing messages (`python extract.py <repo> strings.json`).
- `tools/build-docx.js` — builds the workbook from the keyed list (needs the `docx` npm
  package).

Not translated: data people type in (names, addresses, places, parties, notes), the audit
trail (stored as written), and each client's own branding.
