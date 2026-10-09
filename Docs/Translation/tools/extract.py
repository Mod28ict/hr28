"""
Collects the English text people see in HR28 (screens, buttons, hints, messages, SMS,
rights) into strings.json for the Dhivehi translation workbook.

Heuristic, not a compiler: it reads Razor views (text between tags, placeholder / title /
aria-label / alt / data-confirm attributes, quoted strings in Razor code), the web
controllers and models (messages, validation texts), and the server's user-facing
messages (business-rule errors, SMS texts, rights catalogue). Razor expressions inside a
sentence become {…} so the translator keeps them in place.
"""
import json, os, re, sys
from collections import OrderedDict

ROOT = sys.argv[1]
OUT = sys.argv[2]

def read(path):
    with open(path, encoding="utf-8-sig") as f:
        return f.read()

entries = OrderedDict()          # english -> {"where": [..], "group": str, "kind": str}

def norm(s):
    s = s.replace("&middot;", "·").replace("&amp;", "&").replace("&nbsp;", " ").replace("&quot;", '"')
    s = s.replace("&times;", "×").replace("&hellip;", "…").replace("&rarr;", "→").replace("&copy;", "©")
    s = re.sub(r"\s+", " ", s).strip()
    return s

RAZOR_EXPR = re.compile(r"@\((?:[^()]|\([^()]*\))*\)|@[A-Za-z_][\w.]*(?:\([^)]*\))?(?:\[[^\]]*\])?(?:\.[\w]+(?:\([^)]*\))?)*")

def razor_to_placeholder(s):
    return RAZOR_EXPR.sub("{…}", s)

def useful(text):
    t = text.strip()
    if len(t) < 2:
        return False
    if not re.search(r"[A-Za-z]{2,}", t):
        return False
    if t.startswith(("@", "{", "}", "//", "/*", "*")):
        return False
    # code-looking fragments
    if re.search(r"[;=]\s*$|=>|\bvar\b|\bnew\b \w+\(|\(\)|&&|\|\||==|!=|\.ToString|\bif\s*\(|\belse\b\s*$", t):
        return False
    if re.fullmatch(r"[\w\-]+(\.[\w\-]+)+", t):          # dotted identifiers
        return False
    if re.fullmatch(r"[a-z][a-zA-Z0-9]*", t) and not t in ("or", "of", "to", "by", "and", "in"):  # camelCase ids
        return False
    if "ui-" in t or "h28" in t.lower() and "-" in t:
        return False
    if t in ("{…}", "—", "·"):
        return False
    # leftovers of C# / Razor code spread over several lines
    if re.match(r"^[\w.?]+\s*=\s*\S", t) or t.startswith(("?", ".", "new ", "new{", "(string", "string ", "int ", "bool ", "Model")):
        return False
    if re.search(r"(string|int|bool|DateTime|Guid)\??\s+\w+\s*[,)]", t) or re.fullmatch(r"[\w.]+\)", t):
        return False
    if re.fullmatch(r"[HhMmsdyYt:./ -]+", t):
        return False
    if re.match(r"^\w+Route\(|^: |^\(string", t) or re.search(r"CREATE TABLE|Security:|user-secrets|Key Vault \(production\)", t):
        return False
    return True

CODE_BITS = ('("', '")', '? "', '" :', '$"', 'Contains(', '?page=', '/{', '))', '">', '", "', 'asp-', 'Model.', 'ViewBag', '.Count', 'nameof(')

def add(text, where, group, kind):
    # ["Collector"] = "Adds and updates voters…",  ->  the description
    m = re.match(r'^\["[^"]+"\]\s*=\s*"(.+)",?$', text.strip())
    if m:
        text = m.group(1)
    text = norm(razor_to_placeholder(text))
    if not useful(text):
        return
    if any(bit in text for bit in CODE_BITS) or re.match(r"^\w+/\w+", text):
        return
    e = entries.setdefault(text, {"where": [], "group": group, "kind": kind})
    if where not in e["where"]:
        e["where"].append(where)

STOP_LITERALS = {
    "Index", "Dashboard", "Voters", "Auth", "Login", "Logout", "Users", "Settings", "Reports", "Encounters", "Pledges",
    "Influencers", "QuickEntry", "Profile", "Create", "Edit", "Photo", "Brand", "Logo", "Choose", "Delete",
    "JwtToken", "UserRole", "UserName", "UserId", "CampaignName", "SuccessMessage", "FlashError", "ErrorMessage",
    "Title", "Full", "AddEncounter", "Supporter", "Undecided", "Neutral", "Opponent", "Meet", "Call", "Request",
    "Super Administrator", "M", "F", "Pending", "Fulfilled", "Cancelled", "InProgress",
}

def screen_name(path):
    rel = os.path.relpath(path, os.path.join(ROOT, "HR28.Web", "Views")).replace("\\", "/")
    folder, file = rel.split("/", 1) if "/" in rel else ("", rel)
    file = file.replace(".cshtml", "")
    names = {
        "Shared/_Layout": "Menu, header and footer (every page)",
        "Auth/Login": "Sign-in page", "Auth/VerifyOtp": "SMS code page",
    }
    return names.get(f"{folder}/{file}", f"{folder} › {file.lstrip('_')}")

# ---------- Razor views ----------
views_dir = os.path.join(ROOT, "HR28.Web", "Views")
for dirpath, _, files in os.walk(views_dir):
    for fn in sorted(files):
        if not fn.endswith(".cshtml") or fn in ("_Icon.cshtml", "_ValidationScriptsPartial.cshtml", "_ViewImports.cshtml", "_ViewStart.cshtml"):
            continue
        path = os.path.join(dirpath, fn)
        src = read(path)
        where = screen_name(path)
        group = where.split(" › ")[0] if " › " in where else where

        # Drop comments, styles; keep scripts separately (their messages are user-facing).
        src = re.sub(r"@\*.*?\*@", " ", src, flags=re.S)
        src = re.sub(r"<!--.*?-->", " ", src, flags=re.S)
        src = re.sub(r"<style\b.*?</style>", " ", src, flags=re.S | re.I)
        scripts = re.findall(r"<script\b[^>]*>(.*?)</script>", src, flags=re.S | re.I)
        src_noscript = re.sub(r"<script\b.*?</script>", " ", src, flags=re.S | re.I)

        # Messages written by page scripts ("Copied", "Saving photo…", confirm texts).
        for js in scripts:
            for m in re.finditer(r'"([^"\\\n]{3,})"|\'([^\'\\\n]{3,})\'', js):
                lit = m.group(1) or m.group(2)
                if re.search(r"[A-Z][a-z]+ [a-z]", lit) and not re.search(r"[#.\[\]]|querySelector|=>|^\w+$", lit):
                    add(lit, where, group, "Message")

        # Attributes people can see or hear.
        for m in re.finditer(r'\b(placeholder|title|aria-label|alt|data-confirm)="([^"]*)"', src_noscript):
            val = razor_to_placeholder(m.group(2))
            if m.group(1) == "title" and "ViewData" in m.group(2):
                continue
            add(val, where, group, {"placeholder": "Box hint", "title": "Tooltip", "aria-label": "Screen-reader label",
                                    "alt": "Image description", "data-confirm": "Question"}[m.group(1)])

        # Text between tags.
        text_only = re.sub(r"<[^>]+>", "\n", src_noscript)
        for chunk in text_only.split("\n"):
            c = chunk.strip()
            if not c or c.startswith("@{") or c.startswith("@using") or c.startswith("@model") or c.startswith("@inject"):
                continue
            # razor code lines
            if re.match(r"^(@?(if|else|foreach|for|var|switch|case|return|@section|@await|@Html|}|{)|[}{)]|//)", c):
                continue
            if c.startswith("@:"):
                c = c[2:]
            c = razor_to_placeholder(c)
            add(c, where, group, "Text")

        # Quoted strings in Razor code ("Active" : "Inactive", ViewData["Title"] = "…").
        for m in re.finditer(r'(?<![\w$])\$?"((?:[^"\\\n]|\\.){2,})"', src_noscript):
            lit = m.group(1)
            if lit in STOP_LITERALS or re.search(r"^[\w\-]+$", lit) and not re.match(r"^[A-Z][a-z]{2,}$", lit):
                continue
            if re.search(r"[<>/\\]|^\w+\.\w+|^ui-|^is-|^h28|^#|\(|\)|=", lit):
                continue
            if not re.match(r"^[A-Z0-9\"'“(]", lit):
                continue
            lit = re.sub(r"\{[^}]+\}", "{…}", lit)
            add(lit, where, group, "Text")

# ---------- Web controllers, models, services ----------
def scan_cs(folder, group_label, kind):
    for dirpath, _, files in os.walk(folder):
        if "/obj" in dirpath.replace("\\", "/") or "/bin" in dirpath.replace("\\", "/"):
            continue
        for fn in sorted(files):
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            src = read(path)
            # Audit entries are stored as written (not shown in the reader's language).
            src = re.sub(r"LogAsync\((.*?)\);", "", src, flags=re.S)
            src = re.sub(r"///.*", "", src)          # XML docs
            src = re.sub(r"//.*", "", src)           # comments
            where = f"{group_label} › {fn[:-3]}"
            for m in re.finditer(r'\$?"((?:[^"\\\n]|\\.){6,})"', src):
                lit = m.group(1)
                if " " not in lit.strip():
                    continue
                if not re.match(r"^[A-Z{\"“(]", lit):
                    continue
                if re.search(r"https?://|SELECT |INSERT |UPDATE |DELETE |\bFROM\b|application/|text/|charset|Bearer|swagger|[;<>]|^\{[^}]*\}$|Data Source|Server=", lit):
                    continue
                lit = re.sub(r"\{[^}]+\}", "{…}", lit).replace('\\"', '"')
                add(lit, where, group_label, kind)

scan_cs(os.path.join(ROOT, "HR28.Web", "Controllers"), "Messages after an action (web)", "Message")
scan_cs(os.path.join(ROOT, "HR28.Web", "Models"), "Form names and checks (web)", "Message")
scan_cs(os.path.join(ROOT, "HR28.Web", "Services"), "Messages from the web app", "Message")
scan_cs(os.path.join(ROOT, "HR28.Web", "Filters"), "Messages from the web app", "Message")
scan_cs(os.path.join(ROOT, "HR28.Infrastructure", "Services"), "Messages from the server", "Message")
scan_cs(os.path.join(ROOT, "HR28.Infrastructure", "Helpers"), "Messages from the server", "Message")
scan_cs(os.path.join(ROOT, "HR28.API", "Controllers"), "Messages from the server", "Message")
scan_cs(os.path.join(ROOT, "HR28.Application", "Common"), "Rights and lists", "Message")

with open(OUT, "w", encoding="utf-8") as f:
    json.dump([{"english": k, **v} for k, v in entries.items()], f, ensure_ascii=False, indent=1)

print(len(entries), "strings")
from collections import Counter
print(Counter(v["group"] for v in entries.values()).most_common())
