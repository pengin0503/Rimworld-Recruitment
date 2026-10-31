#!/usr/bin/env python3
"""Static checks for the mod's XML: well-formedness, translation keys and def references.

Usage: python3 tools/check_mod.py [--rimworld-data PATH]

--rimworld-data points at the game's Data folder (Core, Royalty, ...). When given, references to vanilla
defs (research projects, arrival modes, sounds) are checked against it too.
"""
import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LANGUAGES = ["English", "Japanese"]
# Keys built at runtime from an enum value, e.g. "Recruitment_JoinMode_" + mode.
DYNAMIC_KEYS = {
    "Recruitment_JoinMode_": ["Choose", "AutoJoin"],
    "Recruitment_Tab_HowTo_": ["Trader", "Envoy", "CommsConsole"],
}

errors = []


def error(message):
    errors.append(message)


def parse(path):
    try:
        return ET.parse(path).getroot()
    except ET.ParseError as e:
        error(f"{path.relative_to(ROOT)}: XML parse error: {e}")
        return None


def keyed(language):
    keys = {}
    for path in sorted((ROOT / "Languages" / language / "Keyed").glob("*.xml")):
        root = parse(path)
        if root is None:
            continue
        for child in root:
            if child.tag in keys:
                error(f"{language}: duplicate key {child.tag}")
            keys[child.tag] = child.text or ""
    return keys


def placeholders(text):
    return sorted(set(re.findall(r"\{[^{}]+\}", text)))


def check_translations():
    used = set()
    for path in (ROOT / "Source").rglob("*.cs"):
        source = path.read_text(encoding="utf-8")
        for key in re.findall(r'"(Recruitment_[A-Za-z_]+)"', source):
            if key in DYNAMIC_KEYS:
                used.update(key + suffix for suffix in DYNAMIC_KEYS[key])
            elif not key.endswith("_"):
                used.add(key)
    # Def names that happen to look like keys are not translation keys.
    def_names = set(all_mod_defs())
    used -= def_names

    tables = {lang: keyed(lang) for lang in LANGUAGES}
    for lang, table in tables.items():
        for key in sorted(used - table.keys()):
            error(f"{lang}: key used in code but missing: {key}")
        for key in sorted(table.keys() - used):
            error(f"{lang}: key defined but never used: {key}")
    english = tables["English"]
    for lang in LANGUAGES[1:]:
        for key, text in tables[lang].items():
            if key not in english:
                continue
            ours, theirs = set(placeholders(text)), set(placeholders(english[key]))
            # Unknown placeholders are always bugs; dropping a numbered argument is too, but a named one
            # (like a pronoun) may be left out where the language does not need it.
            unknown = ours - theirs
            missing = {p for p in theirs - ours if re.fullmatch(r"\{\d+\}", p)}
            if unknown or missing:
                error(f"{lang}: placeholders of {key} differ from English (unknown {sorted(unknown)}, missing {sorted(missing)})")


def all_mod_defs():
    defs = {}
    for path in sorted((ROOT / "Defs").rglob("*.xml")):
        root = parse(path)
        if root is None:
            continue
        for node in root:
            name = node.findtext("defName")
            if name:
                if name in defs and defs[name][0] == node.tag:
                    error(f"duplicate {node.tag} {name}")
                defs[name] = (node.tag, node)
    return defs


def vanilla_defs(data_path):
    names = set()
    if data_path is None:
        return None
    for path in Path(data_path).glob("*/Defs/**/*.xml"):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for node in root:
            name = node.findtext("defName")
            if name:
                names.add(name)
    return names


def injected(language, def_type, key):
    folder = ROOT / "Languages" / language / "DefInjected" / def_type
    for path in folder.glob("*.xml"):
        root = parse(path)
        if root is not None and root.find(key) is not None:
            return True
    return False


def csharp_types():
    types = set()
    for path in (ROOT / "Source").rglob("*.cs"):
        for name in re.findall(r"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)", path.read_text(encoding="utf-8")):
            types.add("Recruitment." + name)
    return types


def check_defs(data_path):
    defs = all_mod_defs()
    vanilla = vanilla_defs(data_path)
    types = csharp_types()

    def known(name):
        return name in defs or (vanilla is not None and name in vanilla)

    for name, (tag, node) in defs.items():
        for field in ("driverClass", "letterClass", "tabWindowClass"):
            value = node.findtext(field)
            if value and value.startswith("Recruitment.") and value not in types:
                error(f"{tag} {name}: {field} {value} is not a class in Source/")
        if tag.startswith("Recruitment.") and tag not in types:
            error(f"def type {tag} is not a class in Source/")
        if tag == "Recruitment.RecruitmentMethodDef":
            job = node.findtext("startJob")
            if job not in defs or defs[job][0] != "JobDef":
                error(f"{name}: startJob {job} is not a JobDef in Defs/")
            if not node.findall("arrivalModes/li"):
                error(f"{name}: arrivalModes is empty")
            for field in ("label", "description", "arrivalLabel"):
                if not node.findtext(field):
                    error(f"{name}: {field} is not set")
                for lang in LANGUAGES[1:]:
                    if not injected(lang, tag, f"{name}.{field}"):
                        error(f"{lang}: {name}.{field} has no DefInjected translation")
            refs = [li.text for li in node.findall("researchPrerequisites/li")] + [li.text for li in node.findall("arrivalModes/li")]
            if vanilla is not None:
                for ref in refs:
                    if not known(ref):
                        error(f"{name}: unknown def reference {ref}")
        sound = node.findtext("arriveSound")
        if sound and vanilla is not None and not known(sound):
            error(f"{name}: unknown sound {sound}")

    # DefOf fields must match defs.
    defof = (ROOT / "Source" / "Recruitment" / "RecruitmentDefOf.cs").read_text(encoding="utf-8")
    for type_name, field in re.findall(r"public static (\w+) (\w+);", defof):
        if field not in defs:
            error(f"RecruitmentDefOf.{field} has no matching def")

    # DefInjected entries must point at existing defs.
    for lang in LANGUAGES:
        folder = ROOT / "Languages" / lang / "DefInjected"
        if not folder.exists():
            continue
        for path in folder.rglob("*.xml"):
            root = parse(path)
            if root is None:
                continue
            for child in root:
                def_name = child.tag.split(".")[0]
                if def_name not in defs:
                    error(f"{path.relative_to(ROOT)}: {child.tag} refers to unknown def {def_name}")
                elif defs[def_name][0].split(".")[-1] != path.parent.name.split(".")[-1]:
                    error(f"{path.relative_to(ROOT)}: {def_name} is a {defs[def_name][0]}, not a {path.parent.name}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--rimworld-data", help="path to the game's Data folder")
    args = parser.parse_args()
    for path in sorted(ROOT.glob("About/*.xml")):
        parse(path)
    check_defs(args.rimworld_data)
    check_translations()
    if errors:
        print("\n".join(errors))
        print(f"{len(errors)} problem(s) found.")
        return 1
    print("All checks passed" + (" (including vanilla references)." if args.rimworld_data else "."))
    return 0


if __name__ == "__main__":
    sys.exit(main())
