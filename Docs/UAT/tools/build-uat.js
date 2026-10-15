// Builds the CIP UAT & Training document (Word).
// Usage: node build-uat.js <output.docx> <logo.png>
const fs = require("fs");
const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, ShadingType,
  AlignmentType, HeadingLevel, PageOrientation, PageBreak, ImageRun, Header, Footer, PageNumber,
  LevelFormat, BorderStyle, TableOfContents, VerticalAlign,
} = require("docx");

const [, , OUT, LOGO] = process.argv;

const BLUE = "1D4ED8", DARK = "0F172A", GREY = "475569", LIGHT = "EFF6FF", HEAD = "DBEAFE";
const FONT = "Segoe UI";

// ---------- helpers ----------
const t = (text, o = {}) => new TextRun({ text, font: FONT, size: o.size ?? 20, bold: o.bold, italics: o.italics, color: o.color });
const p = (text, o = {}) => new Paragraph({
  spacing: { after: o.after ?? 100, before: o.before ?? 0 },
  alignment: o.align,
  children: Array.isArray(text) ? text : [t(text, o)],
});
const h1 = (text) => new Paragraph({ heading: HeadingLevel.HEADING_1, spacing: { before: 240, after: 120 }, children: [new TextRun({ text, font: FONT, size: 30, bold: true, color: BLUE })] });
const h2 = (text) => new Paragraph({ heading: HeadingLevel.HEADING_2, spacing: { before: 200, after: 80 }, children: [new TextRun({ text, font: FONT, size: 24, bold: true, color: DARK })] });
const bullet = (text, level = 0) => new Paragraph({
  numbering: { reference: "bullets", level }, spacing: { after: 60 },
  children: Array.isArray(text) ? text : [t(text)],
});
const num = (text, ref = "steps") => new Paragraph({ numbering: { reference: ref, level: 0 }, spacing: { after: 60 }, children: Array.isArray(text) ? text : [t(text)] });
const pb = () => new Paragraph({ children: [new PageBreak()] });

function cell(text, width, o = {}) {
  const runs = (Array.isArray(text) ? text : String(text).split("\n")).map((line, i) =>
    new Paragraph({ spacing: { after: 40 }, children: [t(line, { size: o.size ?? 18, bold: o.bold, color: o.color })] }));
  return new TableCell({
    width: { size: width, type: WidthType.DXA },
    shading: o.fill ? { type: ShadingType.CLEAR, color: "auto", fill: o.fill } : undefined,
    margins: { top: 60, bottom: 60, left: 90, right: 90 },
    verticalAlign: o.vcenter ? VerticalAlign.CENTER : undefined,
    children: runs,
  });
}
function table(widths, header, rows, o = {}) {
  const total = widths.reduce((a, b) => a + b, 0);
  return new Table({
    width: { size: total, type: WidthType.DXA },
    columnWidths: widths,
    rows: [
      new TableRow({ tableHeader: true, children: header.map((h, i) => cell(h, widths[i], { bold: true, fill: HEAD, size: 18 })) }),
      ...rows.map((r, ri) => new TableRow({
        cantSplit: true,
        children: r.map((c, i) => cell(c, widths[i], { fill: o.zebra && ri % 2 ? "F8FAFC" : undefined })),
      })),
    ],
  });
}

// ---------- test cases ----------
// [id, title, steps, expected, who]
const modules = [
  ["A", "Signing in and account security", [
    ["A-01", "Sign in with code and SMS", "1. Open https://shamheed.com.\n2. Enter your authorization code (with or without the dash).\n3. Click Send verification code.\n4. Enter the 6-digit SMS code.", "SMS arrives within a minute. After the code you land on your start page (Dashboard or Quick entry). The SMS says never to share the code.", "All"],
    ["A-02", "Wrong authorization code", "Enter a made-up code and click Send verification code.", "A plain message; it does not say whether the code or person exists. No SMS is sent.", "All"],
    ["A-03", "Wrong SMS code", "Request a code, then enter a wrong 6-digit code.", "A plain \"code is not correct\" message with attempts left. After 5 wrong tries that code stops working.", "All"],
    ["A-04", "Expired SMS code", "Request a code, wait longer than the expiry (default 5 minutes), then enter it.", "Message that the code has expired; a new one must be requested.", "All"],
    ["A-05", "Ask for a code too often", "Click Send verification code twice within a minute.", "Second request politely refused (wait 60 seconds). No extra SMS.", "All"],
    ["A-06", "Remember me on this device", "Tick Remember me on this device and sign in. Sign out, then open the site again.", "\"Welcome back, <first name>\" with a Send my sign-in code button: no authorization code needed, the SMS code still is.", "Collector"],
    ["A-07", "\"Not you?\" on a remembered device", "On the Welcome back page click Not you?.", "The device is forgotten; the normal sign-in page with the authorization code returns.", "Collector"],
    ["A-08", "Sign out", "Click your name / Sign out.", "Back on the sign-in page. Pressing Back in the browser does not show the app.", "All"],
    ["A-09", "Idle session (optional, 1 hour)", "Stay signed in without touching the app for 59 minutes.", "\"Are you still there?\" with a 60-second countdown. Stay signed in keeps you in; no answer signs you out with a plain message.", "Any"],
    ["A-10", "Both mobile networks", "Repeat A-01 with one Dhiraagu (7xxxxxx) and one Ooredoo (9xxxxxx) phone.", "Both receive the SMS code.", "Two testers"],
    ["A-11", "New-device sign-in alert", "Sign in on your usual phone. Then sign in on another browser or computer (or a private/incognito window).", "First device: no alert. The other browser: SMS \"new sign-in to your account at HH:MM on <browser>… contact your administrator\". Same browser again: no alert.", "All"],
    ["A-12", "Confirm it's you", "As Administrator open Users (or Settings → System, or download a report).", "\"Confirm it's you\" page: Send me a code → SMS code → Confirm → back on the page you opened. For about 15 minutes no new code is asked; after that it asks again.", "Administrator"],
    ["A-13", "Confirm it's you: wrong code", "On the Confirm page enter a wrong code, then the right one.", "\"That code is not correct. You have N tries left.\" The right code works. A confirmation code cannot be used to sign in.", "Administrator"],
  ]],
  ["B", "Dashboard and start page", [
    ["B-01", "Dashboard figures", "Sign in as Administrator. Look at the Dashboard.", "Greeting with your first name; totals for voters, supporters, undecided, opposition, neutral; constituency/island counts.", "Administrator"],
    ["B-02", "Distribution by area", "Use Show distribution for to pick a constituency.", "Figures change to that constituency only.", "Administrator"],
    ["B-03", "Quick links follow rights", "Sign in as Reporter.", "Only links the role may use are shown (e.g. no Add Voter, no Manage Users).", "Reporter"],
    ["B-04", "Quick entry start page", "In Settings → Roles & rights set a role's start page to Quick entry. Sign in with a user of that role.", "Sign-in opens Quick entry; the Dashboard link is hidden for that user.", "Administrator + Collector"],
  ]],
  ["C", "Voters", [
    ["C-01", "Search within your areas", "Voters → search by name, National ID, phone or island.", "Only voters inside your areas are found. Nothing found shows a friendly \"no voters found\" message.", "Collector"],
    ["C-02", "Search outside your areas", "Search for a voter you know is in another constituency.", "Not found (no information about voters outside your areas).", "Collector"],
    ["C-03", "Filters and sorting", "Use Constituency, Island, Gender, Party, Status, House; click column headings; click an island name.", "List follows each filter; headings sort; clicking an island filters by it. Paging stays fast.", "All with view right"],
    ["C-04", "Default party filter", "Open Voters from the menu.", "Opens on the default party set in Settings → Lists; choosing All parties shows everyone.", "All"],
    ["C-05", "Add a voter", "Add Voter → fill the form with a TEST person (e.g. name \"UAT TEST 1\").", "Saved; appears in the list; Audit Trail has the entry.", "Collector (Add right)"],
    ["C-06", "Duplicate National ID", "Add a voter with a National ID that already exists.", "Friendly message that the ID is already registered; nothing saved.", "Collector"],
    ["C-07", "Edit a voter", "Open the test voter → Edit → change address.", "Saved; Audit Trail shows which fields changed.", "Collector (Edit right)"],
    ["C-08", "Protected fields", "As a non-administrator, edit a voter.", "Constituency and island cannot be changed.", "Collector"],
    ["C-09", "Change support status", "Click the status pill in the list (or in Quick entry) and choose Supporter.", "Status changes at once. Without the \"Change support status\" right the pill is not clickable.", "Role with / without the right"],
    ["C-10", "Voter profile", "Open a voter's profile.", "Details, age and gender, encounters, pledges and influencers (each only with its view right).", "All"],
    ["C-11", "Add / remove photo", "On the list, profile or Quick entry: Add photo (on a phone you may use the camera); then Remove photo.", "Photo appears; remove asks and removes it. Without the photo rights the buttons are hidden.", "Role with photo rights"],
    ["C-12", "Copy ID card number", "Hover a National ID and click the copy icon.", "The ID is copied (\"Copied\").", "All"],
    ["C-13", "Delete a voter", "Delete the test voter \"UAT TEST 1\".", "Asks to confirm; deleted permanently; Audit Trail keeps the name.", "Role with Delete right"],
  ]],
  ["D", "Quick entry", [
    ["D-01", "Fetch a voter by ID card", "Quick entry → type an ID card number in your area → Fetch voter.", "Photo (with right), details and status; Encounters, Pledges and Influencer forms shown in that order.", "Collector"],
    ["D-02", "ID outside your area", "Fetch an ID card from another constituency.", "\"Not found\" (nothing about the person is shown).", "Collector"],
    ["D-03", "Add an encounter", "Fill Encounter type, Date and time, Outcome, Notes → save.", "Saved; page reloads; it appears under the voter.", "Collector"],
    ["D-04", "Add a pledge", "Fill Title, What was pledged, Pledge date, Due date → save.", "Saved; page reloads.", "Collector"],
    ["D-05", "Link an influencer", "Choose an influencer and Relationship to voter → save.", "Linked; linking the same influencer twice is prevented with a clear message.", "Collector (Link right)"],
  ]],
  ["E", "Encounters", [
    ["E-01", "Record an encounter", "Encounters (or a voter) → Add Encounter: type Meet / Call / Request, date, notes.", "Saved and listed.", "Collector"],
    ["E-02", "Response colour", "With the response right, set Supports / Undecided / Does not support.", "Green / yellow / red shown. Without the right the response field is not offered.", "Role with / without response right"],
    ["E-03", "Search and filter", "Search encounters by voter name, National ID or notes; filter by type.", "Only matching encounters in your areas.", "All with view right"],
    ["E-04", "Edit an encounter", "Edit the test encounter's notes.", "Saved; Audit Trail shows \"notes\" changed (not the text).", "Role with Edit right"],
  ]],
  ["F", "Pledges", [
    ["F-01", "Add a pledge", "Pledges → Add Pledge for the test voter.", "Saved and listed.", "Collector"],
    ["F-02", "Update / fulfil", "Update Pledge → mark fulfilled.", "Status and fulfilled date shown.", "Role with Edit right"],
    ["F-03", "Overdue", "Create a pledge with a due date in the past (test record).", "Shown as overdue.", "Collector"],
  ]],
  ["G", "Influencers", [
    ["G-01", "List and search", "Influencers → search by name, ID, phone or island; filter Constituency, Island, Category.", "All influencers are listed for everyone (influencers are shared).", "All"],
    ["G-02", "Add an influencer", "Add Influencer (TEST person) with a category.", "Saved; duplicate National ID refused with a friendly message.", "Role with Add right"],
    ["G-03", "Linked voters", "Click \"N linked voters\".", "Only voters in your areas are listed; links outside them are counted, never shown.", "Collector"],
    ["G-04", "Delete an influencer", "Delete the TEST influencer.", "Confirmation shows how many voter links will go; then deleted; Audit Trail keeps name and ID.", "Role with Delete right"],
  ]],
  ["H", "Reports", [
    ["H-01", "Open reports", "Reports → Constituency Summary, Pledges, Top Influencers.", "Each opens with figures for your areas. Without the Reports right the menu is hidden.", "Reporter"],
    ["H-02", "Download and print", "Download CSV and Print on a report.", "CSV opens in Excel with dates in Maldives time; print layout is clean. Without the Download right the buttons are hidden.", "Role with / without Download right"],
  ]],
  ["I", "Uploads (Administrators only)", [
    ["I-01", "Upload voter list", "Voters → Upload voter list → choose a small TEST .xlsx.", "Shows counts added / updated / skipped; one Audit Trail entry. Files over 20 MB or other types refused.", "Administrator"],
    ["I-02", "Upload membership list", "Voters → Upload membership list → choose party and a TEST file.", "Counts shown; only empty birth date / mobile / gender are filled.", "Administrator"],
    ["I-03", "Not for other roles", "As Collector, look for the upload options.", "Not shown.", "Collector"],
  ]],
  ["J", "Users (Administrators only)", [
    ["J-01", "Create a user", "Users → Add user with a real phone you can check.", "\"User created\": the account starts inactive; steps to give a role and area.", "Administrator"],
    ["J-02", "Activation needs a role", "Try to activate before giving a role.", "Refused with a clear message.", "Administrator"],
    ["J-03", "Roles and areas", "Give the user a role (tick list) and add an area.", "Saved; Audit Trail records both.", "Administrator"],
    ["J-04", "Activate → welcome SMS", "Activate the user.", "The user gets a welcome SMS with their code. The administrator sees only \"sent by SMS (•••xxx)\" — never the code.", "Administrator + new user"],
    ["J-05", "Reset code → SMS only", "Users → Reset code for the test user.", "Old code stops at once; new code arrives only by SMS; the administrator never sees it. Remembered devices are forgotten.", "Administrator + user"],
    ["J-06", "Wrong number on reset", "Set an invalid number (e.g. 8 digits) and Reset code; then fix it and reset again.", "Red message: SMS not sent, account has no code (\"No code yet: use Reset code\"). After fixing, reset works.", "Administrator"],
    ["J-07", "Deactivate → SMS", "Switch the test user to Inactive.", "The user gets \"your account has been deactivated\" by SMS and can no longer sign in.", "Administrator + user"],
    ["J-08", "Reactivate", "Switch the test user back to Active.", "\"Active again\" SMS; same code works.", "Administrator + user"],
    ["J-09", "End all sessions", "Edit → End all sessions for a signed-in user.", "Their next click goes to the sign-in page (\"session has ended\").", "Administrator + user"],
    ["J-10", "Forget remembered devices", "Edit → Forget remembered devices.", "The user must type their authorization code again on every device.", "Administrator + user"],
    ["J-11", "Extra rights", "Users → Roles → Extra rights: give one right to one person.", "Only that person gains it; Audit Trail records it.", "Administrator"],
    ["J-12", "Safety rules", "Try to deactivate your own account / the only active Administrator.", "Refused with a clear message.", "Administrator"],
    ["J-13", "Phone number change", "Change the test user's mobile number.", "The old number gets a warning SMS; Audit Trail records the change.", "Administrator"],
  ]],
  ["K", "Settings", [
    ["K-01", "Appearance", "Settings → Appearance → Dark.", "Whole app switches to dark; remembered next time.", "All"],
    ["K-02", "Branding", "Settings → System → Branding: campaign name, short name, tagline, logo.", "Shown on sign-in pages, sidebar, page titles and the page loader. \"Powered by CIP\" stays in the footer.", "Administrator"],
    ["K-03", "Sign-in security", "Change OTP expiry, maximum attempts, remember-device days.", "Saved; applies to the next code / sign-in. Audit Trail records it.", "Administrator"],
    ["K-04", "Constituencies and islands", "Add a TEST island to a constituency; rename it; delete it.", "Each island belongs to one constituency; one with voters can't be moved.", "Administrator"],
    ["K-05", "Lists", "Lists → political parties and influencer categories: add / rename / delete a TEST entry.", "Works; an entry in use can't be deleted.", "Administrator"],
    ["K-06", "Roles & rights", "Create a custom role; tick rights in the grid; set start page and voter-profile view.", "Users with the role get exactly those rights within a minute; built-in roles can't be renamed or deleted.", "Administrator"],
  ]],
  ["L", "Audit Trail", [
    ["L-01", "Review actions", "Audit Trail → filter by date, record type, action.", "Every test action above is listed with who, when and what changed. No codes or passwords appear.", "Administrator"],
  ]],
  ["M", "Access control (cross-checks)", [
    ["M-01", "Reporter is read-only", "As Reporter try to add or edit anything.", "No add / edit buttons; direct attempts are refused.", "Reporter"],
    ["M-02", "No administration for others", "As Collector look for Users and Settings → System.", "Not available.", "Collector"],
    ["M-03", "Rights change live", "Remove a right from a signed-in user's role.", "Within about a minute the button disappears and the action is refused.", "Administrator + user"],
  ]],
  ["N", "Phones and everyday use", [
    ["N-01", "Phone layout", "Use the app on a phone (mobile data and Wi-Fi).", "All pages readable without sideways scrolling; menus open from the top button.", "Collector"],
    ["N-02", "Camera photo", "On a phone, Quick entry → Add photo → take a picture.", "Photo is shrunk automatically and saved.", "Role with photo rights"],
    ["N-03", "Outside the Maldives (optional)", "Open the site from abroad / a foreign VPN.", "A short \"verify you are human\" check first, then the normal sign-in.", "Any"],
  ]],
];

const total = modules.reduce((n, m) => n + m[2].length, 0);

// ---------- content ----------
const intro = [];
intro.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 1600, after: 400 },
  children: [new ImageRun({ type: "png", data: fs.readFileSync(LOGO), transformation: { width: 420, height: 79 } })] }));
intro.push(p([t("User Acceptance Testing (UAT)", { size: 44, bold: true, color: DARK })], { align: AlignmentType.CENTER, after: 120 }));
intro.push(p([t("and User Training Guide", { size: 32, color: BLUE })], { align: AlignmentType.CENTER, after: 600 }));
intro.push(p([t("Deployment: Hithaai Roohun 2028 — https://shamheed.com", { size: 22, color: GREY })], { align: AlignmentType.CENTER }));
intro.push(p([t("Version 1.1 — 15 October 2026", { size: 22, color: GREY })], { align: AlignmentType.CENTER }));
intro.push(p([t("Prepared by: Ahmed Rasheed (Designed and Developed by)", { size: 22, color: GREY })], { align: AlignmentType.CENTER, after: 1200 }));
intro.push(p([t("Confidential — contains no personal data. Do not write real voters' details, codes or passwords in this document.", { size: 18, italics: true, color: GREY })], { align: AlignmentType.CENTER }));
intro.push(pb());

intro.push(h1("Contents"));
intro.push(new TableOfContents("Contents", { hyperlink: true, headingStyleRange: "1-2" }));
intro.push(p([t("(In Word: right-click the table and choose Update Field to fill in page numbers.)", { size: 16, italics: true, color: GREY })]));
intro.push(pb());

intro.push(h1("1. About this document"));
intro.push(p("This document does two jobs: it is the checklist the client's team uses to confirm that the platform works as agreed (User Acceptance Testing, UAT), and the guide for training each group of users. Every test is written in plain steps, so the people who will use the system day to day can run them themselves."));
intro.push(h2("1.1 What is tested"));
intro.push(p(`${total} tests in ${modules.length} areas: signing in and security, Dashboard, Voters, Quick entry, Encounters, Pledges, Influencers, Reports, uploads, Users, Settings, Audit Trail, access control and phone use.`));
intro.push(h2("1.2 Not part of this UAT"));
[
  "The Dhivehi interface (waiting for the translated workbook and fonts).",
  "Passkeys (fingerprint / face sign-in) for administrators — planned for later.",
  "Paid modules (Election-day operations, Bulk SMS campaigns, Case management, Field teams, Events, Advanced analytics, Security pack).",
  "Server, backup and Cloudflare settings — done and checked by the developer.",
].forEach((x) => intro.push(bullet(x)));
intro.push(h2("1.3 Important: the system is live"));
intro.push(p([t("Testing happens on the live system with the real voter registry. Please follow these rules:", { bold: true })]));
[
  "Never change or delete a real voter, influencer or pledge to \"see what happens\". Create TEST records instead and name them clearly, e.g. \"UAT TEST 1\".",
  "Delete every TEST record at the end of the day (tests C-13, G-04 and similar), and note it in the results.",
  "Every SMS is real and uses the SMS balance: don't repeat sign-in or reset tests more than needed.",
  "Never write authorization codes, SMS codes or anyone's personal details in this document or in chat groups.",
  "If something looks wrong, stop, write it in the Issue log (section 6) with the time, and carry on with the next test.",
].forEach((x) => intro.push(bullet(x)));

intro.push(h1("2. Before you start"));
intro.push(h2("2.1 Checklist"));
[
  "Each tester has an account with the right role and area (section 2.2), and the correct 7-digit mobile number (starting 7 or 9).",
  "Each tester has received their authorization code by SMS (activation or Reset code). Administrators never see codes.",
  "Enough SMS balance in the Dhiraagu Bulk SMS account.",
  "A phone (Dhiraagu and Ooredoo if possible) and a computer with an up-to-date browser (Chrome, Edge, Safari or Firefox).",
  "A small TEST Excel file for the upload tests (a few made-up rows only).",
  "This document printed, or open in Word, to record results.",
].forEach((x) => intro.push(bullet(x)));
intro.push(h2("2.2 Testers and test accounts"));
intro.push(p("Fill in before the session. One person can test several roles one after the other (sign out in between)."));
intro.push(table([2100, 2300, 2600, 2000], ["Role", "Tester name", "Area(s) (constituency / island)", "Phone network"], [
  ["Administrator", "", "All (no area needed)", ""],
  ["National Administrator", "", "", ""],
  ["Constituency Administrator", "", "", ""],
  ["Island Administrator", "", "", ""],
  ["Collector", "", "", ""],
  ["Reporter", "", "", ""],
]));
intro.push(h2("2.3 How to record results"));
intro.push(table([1700, 7300], ["Mark", "Meaning"], [
  ["P — Pass", "The expected result happened exactly."],
  ["F — Fail", "Something different happened. Write a short note and add it to the Issue log (section 6)."],
  ["B — Blocked", "Could not be tested (e.g. no SMS balance, missing right). Say why."],
  ["N/A", "Not relevant for this deployment."],
]));
intro.push(p(""));
intro.push(p("Sensitive areas (Users, Settings → System / places / lists / Roles & rights, report downloads, uploads) first ask you to \"Confirm it's you\" with a code sent by SMS; one confirmation lasts about 15 minutes."));
intro.push(p("Rights mentioned in a test (e.g. \"Edit right\", \"photo rights\") are set by the Administrator in Settings → Roles & rights or for one person in Users → Roles → Extra rights. Where a test says \"with / without the right\", test both: the button should appear only for the person who has the right."));

// ---------- test scripts (landscape) ----------
const scripts = [];
scripts.push(h1("3. Test scripts"));
scripts.push(p("Work through the areas in order; sign-in (A) comes first. Tick the result and add notes. Test IDs are used in the Issue log."));
const W = [800, 1900, 4400, 3700, 1500, 800, 1600];
for (const [code, name, cases] of modules) {
  scripts.push(h2(`${code}. ${name}`));
  scripts.push(table(W, ["ID", "Test", "Steps", "Expected result", "Who", "Result", "Notes"],
    cases.map(([id, title, steps, expected, who]) => [id, title, steps, expected, who, "", ""]), { zebra: true }));
}

// ---------- summary, issues, training, sign-off ----------
const tail = [];
tail.push(h1("4. Results summary"));
tail.push(table([3600, 1300, 1300, 1300, 1300, 1600], ["Area", "Tests", "Pass", "Fail", "Blocked", "Tested by"],
  modules.map(([c, n, cs]) => [`${c}. ${n}`, String(cs.length), "", "", "", ""]).concat([["Total", String(total), "", "", "", ""]])));

tail.push(h1("5. Clean-up after testing"));
[
  "Delete all TEST voters, encounters, pledges and influencers (search for \"UAT TEST\").",
  "Delete TEST islands, list entries and custom roles created for testing.",
  "Deactivate or delete TEST user accounts (deactivation sends them an SMS).",
  "Restore any Settings changed for testing (OTP expiry, remember-device days, branding).",
  "Check the Audit Trail: every clean-up step is listed.",
].forEach((x) => tail.push(bullet(x)));

tail.push(pb());
tail.push(h1("6. Issue log"));
tail.push(p("One line per problem. Severity: High = can't do the job / wrong data or security; Medium = works with a workaround; Low = wording, layout, cosmetic."));
tail.push(table([900, 1000, 1400, 4600, 1100, 1400, 1600, 1600], ["No.", "Test ID", "Date & time", "What happened (steps, message on screen)", "Severity", "Reported by", "Fixed in version", "Re-tested (P/F)"],
  Array.from({ length: 12 }, (_, i) => [String(i + 1), "", "", "", "", "", "", ""])));

tail.push(pb());
tail.push(h1("7. User training plan"));
tail.push(p("Short, hands-on sessions per group. Each session ends with the trainees running the listed tests themselves on their own phones and accounts."));
tail.push(table([2300, 1300, 5300, 3300, 1600], ["Group", "Length", "Topics", "Hands-on tests", "Trainer"], [
  ["Everyone (start here)", "30 min", "Signing in with the authorization code and SMS code; Remember me (only on your own device); \"Not you?\"; signing out; the 1-hour idle question; never sharing codes; who to call when locked out.", "A-01, A-03, A-06, A-07, A-08", ""],
  ["Collectors", "60 min", "Quick entry by ID card; recording encounters (Meet / Call / Request) with short factual notes; pledges; linking influencers; changing support status; photos with the phone camera; searching voters in your areas.", "D-01 – D-05, C-01 – C-03, C-09, C-11, E-01, F-01, N-01, N-02", ""],
  ["Area administrators (Constituency / Island)", "60 min", "Everything for Collectors plus editing voters, updating pledges, reviewing encounters and the Dashboard for their areas.", "B-01, B-02, C-05 – C-10, E-03, E-04, F-02, F-03, G-01 – G-03", ""],
  ["Reporters", "30 min", "Opening reports, filtering, downloading CSV and printing; data stays inside your areas.", "H-01, H-02, M-01", ""],
  ["Administrators", "2 hours", "Users (create, roles, areas, activate, Reset code, deactivate, end sessions, extra rights); Roles & rights and start pages; Settings (branding, sign-in security, constituencies and islands, lists); uploads; Audit Trail; what to do when someone loses their phone.", "Sections I, J, K, L, M", ""],
]));
tail.push(h2("7.1 Key messages for every trainee"));
[
  "Your authorization code and SMS codes are personal. Campaign staff will never ask for them — not even administrators, who can't see them.",
  "Lost phone or changed number: tell your administrator at once. They will change the number and use Reset code.",
  "Remember me only on your own phone or computer, never on a shared or public one.",
  "Write short, factual notes only. Never record health, money, family matters, allegations or ID card copies.",
  "You only see voters in your own areas; that is by design.",
  "Election-day records show only whether someone voted, never how.",
].forEach((x) => tail.push(bullet(x)));
tail.push(pb());
tail.push(h2("7.2 Quick reference card: signing in"));
["Open https://shamheed.com on your phone or computer.",
 "Type your authorization code (from the SMS you received, e.g. ABCD-EFGH; the dash and capitals don't matter).",
 "Tap Send verification code and wait for the SMS.",
 "Type the 6-digit code within 5 minutes.",
 "On your own phone you may tick Remember me: next time you only need the SMS code.",
 "Problems? \"Code not correct\" → check and try again (5 tries). No SMS → wait 1 minute and ask again, or call your administrator.",
].forEach((x) => tail.push(num(x, "card")));

tail.push(pb());
tail.push(h1("8. Acceptance and sign-off"));
tail.push(p("By signing, the client confirms that the tests above were carried out, the results are recorded in this document, and the platform is accepted for use — subject to any open issues listed in section 6 and their agreed fix dates."));
tail.push(table([3000, 3300, 2700, 2000], ["Role", "Name", "Signature", "Date"], [
  ["Client representative (Owner of the campaign)", "", "", ""],
  ["Client Administrator", "", "", ""],
  ["UAT coordinator", "", "", ""],
  ["Developer (Ahmed Rasheed)", "", "", ""],
]));
tail.push(p(""));
tail.push(p([t("Open issues accepted at sign-off:", { bold: true })]));
for (let i = 0; i < 4; i++) tail.push(p("…………………………………………………………………………………………………………………………", { color: GREY }));

// ---------- document ----------
const footer = new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [
  t("Campaign Intelligence Platform (CIP) — UAT & Training Guide v1.1 — page ", { size: 16, color: GREY }),
  new TextRun({ children: [PageNumber.CURRENT], font: FONT, size: 16, color: GREY }),
] })] });
const A4 = { width: 11906, height: 16838 };
const margins = { top: 1100, bottom: 1100, left: 1100, right: 1100 };

const doc = new Document({
  creator: "Ahmed Rasheed", title: "CIP UAT and Training Guide",
  styles: { default: { document: { run: { font: FONT, size: 20 } } } },
  numbering: { config: [
    { reference: "bullets", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 400, hanging: 260 } } } }] },
    { reference: "steps", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 400, hanging: 300 } } } }] },
    { reference: "card", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 400, hanging: 300 } } } }] },
  ] },
  sections: [
    { properties: { page: { size: A4, margin: margins } }, footers: { default: footer }, children: intro },
    { properties: { page: { size: { ...A4, orientation: PageOrientation.LANDSCAPE }, margin: margins } }, footers: { default: footer }, children: scripts },
    { properties: { page: { size: { ...A4, orientation: PageOrientation.LANDSCAPE }, margin: margins } }, footers: { default: footer }, children: tail },
  ],
});

Packer.toBuffer(doc).then((buf) => { fs.writeFileSync(OUT, buf); console.log(`written ${OUT}: ${total} tests in ${modules.length} areas`); });
