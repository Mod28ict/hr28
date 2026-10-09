// Builds the Dhivehi translation workbook (Word) from workbook.json.
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, ShadingType,
  HeadingLevel, AlignmentType, PageOrientation, BorderStyle, LevelFormat, Footer, PageNumber,
  TableLayoutType, VerticalAlign,
} = require("docx");

const data = JSON.parse(fs.readFileSync(path.join(__dirname, "..", "dhivehi-keys.json"), "utf8"));
const out = process.argv[2];

const INK = "1F2A37", MUTED = "5B6675", ACCENT = "1D6B55", BAND = "F1F6F4", RULE = "C9D6D0", FILL = "FFF8E6";
const LATIN = "Calibri", THAANA = "MV Boli";

const border = { style: BorderStyle.SINGLE, size: 4, color: RULE };
const borders = { top: border, bottom: border, left: border, right: border };

// A4 landscape: 16838 x 11906, margins 850 (1.5 cm) -> content width 15138
const W = { key: 1050, en: 5300, dv: 6250, note: 2538 };
const TABLE_W = W.key + W.en + W.dv + W.note;

const p = (text, opts = {}) => new Paragraph({
  spacing: { after: 80, ...(opts.spacing || {}) },
  alignment: opts.alignment,
  children: [new TextRun({ text, font: LATIN, size: opts.size || 21, bold: opts.bold, color: opts.color || INK, italics: opts.italics })],
});

const dvParagraph = (text = "") => new Paragraph({
  bidirectional: true,
  alignment: AlignmentType.RIGHT,
  children: [new TextRun({ text, font: { ascii: LATIN, hAnsi: LATIN, cs: THAANA }, size: 22, sizeComplexScript: 26, rightToLeft: true })],
});

function cell(children, width, opts = {}) {
  return new TableCell({
    width: { size: width, type: WidthType.DXA },
    borders,
    verticalAlign: VerticalAlign.TOP,
    shading: opts.fill ? { fill: opts.fill, type: ShadingType.CLEAR, color: "auto" } : undefined,
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    children,
  });
}

function headerRow(labels) {
  return new TableRow({
    tableHeader: true,
    children: labels.map(([label, width]) => cell([
      new Paragraph({ children: [new TextRun({ text: label, font: LATIN, size: 19, bold: true, color: "FFFFFF" })] }),
    ], width, { fill: ACCENT })),
  });
}

function stringsTable(items) {
  const rows = [headerRow([["Key", W.key], ["English", W.en], ["Dhivehi  (ދިވެހި)", W.dv], ["Where / type", W.note]])];
  items.forEach((it, i) => {
    rows.push(new TableRow({
      cantSplit: true,
      children: [
        cell([new Paragraph({ children: [new TextRun({ text: it.key, font: "Consolas", size: 16, color: MUTED })] })], W.key, { fill: i % 2 ? BAND : undefined }),
        cell([new Paragraph({ children: [new TextRun({ text: it.english, font: LATIN, size: 20, color: INK })] })], W.en, { fill: i % 2 ? BAND : undefined }),
        cell([dvParagraph()], W.dv, { fill: FILL }),
        cell([new Paragraph({ children: [new TextRun({ text: `${it.where}${it.kind && it.kind !== "Text" ? ` · ${it.kind}` : ""}`, font: LATIN, size: 16, color: MUTED })] })], W.note, { fill: i % 2 ? BAND : undefined }),
      ],
    }));
  });
  return new Table({ width: { size: TABLE_W, type: WidthType.DXA }, columnWidths: [W.key, W.en, W.dv, W.note], layout: TableLayoutType.FIXED, rows });
}

function questionTable(rows) {
  const qW = 6200, aW = TABLE_W - qW;
  return new Table({
    width: { size: TABLE_W, type: WidthType.DXA },
    columnWidths: [qW, aW],
    layout: TableLayoutType.FIXED,
    rows: [
      headerRow([["Question", qW], ["Your answer", aW]]),
      ...rows.map(([q, hint]) => new TableRow({
        cantSplit: true,
        children: [
          cell([
            new Paragraph({ children: [new TextRun({ text: q, font: LATIN, size: 20, bold: true, color: INK })] }),
            ...(hint ? [new Paragraph({ children: [new TextRun({ text: hint, font: LATIN, size: 17, color: MUTED })] })] : []),
          ], qW),
          cell([new Paragraph({ children: [new TextRun({ text: "", font: LATIN, size: 20 })] })], aW, { fill: FILL }),
        ],
      })),
    ],
  });
}

const GLOSSARY = [
  "Voter", "Voters list", "Voter profile", "ID card number", "Encounter", "Pledge", "Influencer", "Constituency", "Island",
  "Area (the constituencies or islands a person works in)", "Role", "Right (permission)", "Administrator", "National Administrator",
  "Constituency Administrator", "Island Administrator", "Collector", "Reporter", "Authorization code", "SMS code (verification code)",
  "Sign in", "Sign out", "Session", "Remember me on this device", "Dashboard", "Quick entry", "Support status", "Supporter",
  "Undecided", "Neutral", "Opponent", "Response: Supports / Undecided / Does not support", "Meet / Call / Request",
  "Political party", "Report", "Audit trail", "Settings", "Photo", "Campaign", "Upload", "Save", "Cancel", "Delete", "Edit",
];

function glossaryTable() {
  const tW = 6000, dW = 6100, nW = TABLE_W - tW - dW;
  return new Table({
    width: { size: TABLE_W, type: WidthType.DXA },
    columnWidths: [tW, dW, nW],
    layout: TableLayoutType.FIXED,
    rows: [
      headerRow([["English term", tW], ["Dhivehi  (ދިވެހި)", dW], ["Notes", nW]]),
      ...GLOSSARY.map((term, i) => new TableRow({
        cantSplit: true,
        children: [
          cell([new Paragraph({ children: [new TextRun({ text: term, font: LATIN, size: 20, bold: true, color: INK })] })], tW, { fill: i % 2 ? BAND : undefined }),
          cell([dvParagraph()], dW, { fill: FILL }),
          cell([new Paragraph({ children: [new TextRun({ text: "", font: LATIN, size: 18 })] })], nW, { fill: i % 2 ? BAND : undefined }),
        ],
      })),
    ],
  });
}

const H1 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, spacing: { before: 120, after: 160 }, children: [new TextRun({ text: t })] });
const H2 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_2, spacing: { before: 240, after: 120 }, children: [new TextRun({ text: t })] });
const num = (text) => new Paragraph({ numbering: { reference: "steps", level: 0 }, spacing: { after: 70 }, children: [new TextRun({ text, font: LATIN, size: 21, color: INK })] });
const bullet = (text) => new Paragraph({ numbering: { reference: "dots", level: 0 }, spacing: { after: 60 }, children: [new TextRun({ text, font: LATIN, size: 21, color: INK })] });

const intro = [
  new Paragraph({ spacing: { after: 60 }, children: [new TextRun({ text: "Dhivehi translation", font: LATIN, size: 44, bold: true, color: ACCENT })] }),
  new Paragraph({ spacing: { after: 240 }, children: [new TextRun({ text: `All the words and messages people see in the app (${data.total.toLocaleString("en")} lines), to translate into Dhivehi (ދިވެހި). Prepared 9 October 2026.`, font: LATIN, size: 22, color: MUTED })] }),

  H2("How to fill this in"),
  num("Type the Dhivehi in the yellow \"Dhivehi\" column. Please don't change the other columns: the Key lets the system put each translation in the right place."),
  num("{…} marks something the app fills in, such as a name, a number or a date. Keep it once in your sentence, wherever it reads naturally in Dhivehi."),
  num("Some short lines are pieces of one sentence that is split around a link or a bold word (for example \"Showing\", \"to\", \"of\"). Translate them so they read well in that order, and add a note if the order needs to change."),
  num("If a line should stay in English (for example \"OTP\", or an example like A123456), copy the English or write a note in your own words."),
  num("Please use the same Dhivehi word for the same term everywhere. Fill in the glossary first; it lists the main terms."),
  num("You can leave a line empty if you're unsure; it will then stay in English until it's filled in."),

  H2("Not translated"),
  bullet("Anything typed into the app: voters' names, addresses, island, constituency and party names, notes and remarks."),
  bullet("The audit trail: it stores what happened, in English, at the moment it happened."),
  bullet("Your campaign's own name, short name and tagline (you set these yourself in Settings)."),

  H2("Fonts and language settings"),
  p("Please answer these so the Dhivehi screens look right. Attach the font files when you send this back.", { color: MUTED }),
  questionTable([
    ["Font for Dhivehi text (body)", "For example Faruma, MV Waheed or MV Typewriter. Please attach the font file (.ttf, .otf or .woff2)."],
    ["Font for Dhivehi headings (if different)", ""],
    ["May the fonts be used on a website?", "Some fonts are free only for personal use; please confirm the licence allows web use."],
    ["Numbers", "Show numbers as 0–9, as now? (Recommended: yes.)"],
    ["Dates", "How should dates read in Dhivehi, for example 9 October 2026? Write an example."],
    ["Default language for new users", "Dhivehi or English. Each person can change their own later."],
    ["Language of the SMS messages (sign-in code, welcome)", "Follow the person's language, always Dhivehi, or always English?"],
    ["Language of the sign-in page (before anyone has signed in)", "Dhivehi, English, or a switch on the page?"],
  ]),

  H2("Glossary (please fill in first)"),
  p("The main terms, so the same word is used everywhere.", { color: MUTED }),
  glossaryTable(),
];

const body = [];
for (const s of data.sections) {
  body.push(H1(`${s.title}  (${s.items.length})`));
  body.push(stringsTable(s.items));
}

const doc = new Document({
  creator: "HR28",
  title: "Dhivehi translation",
  description: "Translation workbook for the Dhivehi interface",
  styles: {
    default: { document: { run: { font: LATIN, size: 21, color: INK } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { font: LATIN, size: 30, bold: true, color: ACCENT }, paragraph: { spacing: { before: 120, after: 160 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { font: LATIN, size: 25, bold: true, color: INK }, paragraph: { spacing: { before: 240, after: 120 }, outlineLevel: 1 } },
    ],
  },
  numbering: {
    config: [
      { reference: "steps", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 400, hanging: 300 } } } }] },
      { reference: "dots", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 400, hanging: 300 } } } }] },
    ],
  },
  sections: [{
    properties: {
      page: {
        size: { width: 11906, height: 16838, orientation: PageOrientation.LANDSCAPE },
        margin: { top: 850, bottom: 850, left: 850, right: 850 },
      },
    },
    footers: {
      default: new Footer({
        children: [new Paragraph({
          alignment: AlignmentType.RIGHT,
          children: [
            new TextRun({ text: "Dhivehi translation · page ", font: LATIN, size: 16, color: MUTED }),
            new TextRun({ children: [PageNumber.CURRENT], font: LATIN, size: 16, color: MUTED }),
          ],
        })],
      }),
    },
    children: [...intro, ...body],
  }],
});

Packer.toBuffer(doc).then((buf) => { fs.writeFileSync(out, buf); console.log("written", out, buf.length); });
