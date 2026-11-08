"""Synchronize sourced classic quests; the captured revision is the allowlist.

This command does not fetch a current mission list or change existing quest IDs.
Run from the repository root after updating the factual specifications.
"""
import copy
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def normalized(title):
    return re.sub(r"[^a-z0-9]", "", title.removeprefix("Quest/").lower())


def synchronize():
    reference = ROOT / "config/reference"
    inventory_path = reference / "mission-list-63014.json"
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    specs = json.loads((reference / "classic-quest-specs.json").read_text(encoding="utf-8"))["quests"]
    allowlist = {(row["level"], normalized(row["title"])) for row in inventory["missions"]}
    catalog_path = ROOT / "config/quests.xml"
    tree = ET.parse(catalog_path)
    catalog = tree.getroot()
    existing = {(int(q.get("minLevel", "1")), normalized(q.get("title", "")), int(q.get("factionId", "0"))): q for q in catalog}
    generated_ids = set()
    for spec in specs:
        key = (spec["level"], normalized(spec["title"]))
        if key not in allowlist:
            raise ValueError("Quest is outside revision 63014: " + spec["title"])
        source_index = next(i for i, row in enumerate(inventory["missions"]) if (row["level"], normalized(row["title"])) == key)
        attributes = copy.deepcopy(spec.get("attributes", {}))
        excluded = attributes.pop("excludedFaction", 0)
        per_faction = attributes.pop("playerFactionVariants", 0)
        target_variants = attributes.pop("targetFactionVariants", 0)
        factions = [f for f in (1, 2, 3) if f != excluded] if excluded or per_faction or target_variants else [0]
        for faction in factions:
            targets = [f for f in (1, 2, 3) if f != faction] if target_variants else [0]
            for target in targets:
                conditions = copy.deepcopy(spec["conditions"])
                for condition in conditions:
                    slot = condition.pop("ownerSlot", None)
                    if slot:
                        condition["mapOwner"] = [f for f in (1, 2, 3) if f != faction][slot - 1]
                    if target:
                        condition["targetFactionId"] = target
                old = existing.get((*key, faction)) if not target else None
                quest_id = int(old.get("id")) if old is not None else 20000 + source_index * 100 + faction * 10 + target
                generated_ids.add(quest_id)
                if old is not None:
                    catalog.remove(old)
                # Regeneration replaces only records owned by this synchronizer.
                for previous in list(catalog):
                    if int(previous.get("id")) == quest_id:
                        catalog.remove(previous)
                q = ET.SubElement(catalog, "quest", {
                    "id": str(quest_id), "title": spec["title"], "titleKey": spec.get("titleKeysByTargetFaction", {}).get(str(target), spec.get("titleKey", spec["title"])),
                    "description": "; ".join(f"{c['type']}: {c['target']}" for c in conditions),
                    "sortOrder": str(source_index + 1), "minLevel": str(spec["level"]),
                    "priority": "0", "factionId": str(faction), "sourceRevision": "63014",
                    "managedBy": "classicQuestSync",
                    "source": spec["source"], **{k: str(v) for k, v in attributes.items()},
                })
                cs = ET.SubElement(q, "conditions")
                for i, condition in enumerate(conditions, 1):
                    ET.SubElement(cs, "condition", {
                        "id": "objective_" + str(i), "wireId": str(i),
                        "descriptionKey": objective_text(condition) + (restriction_text(attributes) if i == 1 else ""),
                        **{k: str(v) for k, v in condition.items()},
                    })
                rs = ET.SubElement(q, "rewards")
                for reward in spec["rewards"]:
                    ET.SubElement(rs, "reward", {k: str(v) for k, v in reward.items()})
    for previous in list(catalog):
        owned = previous.get("managedBy") == "classicQuestSync" or (previous.get("sourceRevision") == "63014" and int(previous.get("id")) >= 20000)
        if owned and int(previous.get("id")) not in generated_ids:
            catalog.remove(previous)
    catalog[:] = sorted(catalog, key=lambda q: (int(q.get("minLevel", "1")), int(q.get("sortOrder", "0")), int(q.get("id"))))
    ET.indent(tree, space="  ")
    tree.write(catalog_path, encoding="utf-8", xml_declaration=True)
    for row in inventory["missions"]:
        matches = [q for q in catalog if int(q.get("minLevel", "1")) == row["level"] and normalized(q.get("title", "")) == normalized(row["title"])]
        row["questIds"] = [int(q.get("id")) for q in matches]
        row["inCatalog"] = bool(matches)
    inventory_path.write_text(json.dumps(inventory, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    missing = [row for row in inventory["missions"] if not row["inCatalog"]]
    (reference / "classic-quest-pending.json").write_text(json.dumps({"revision": 63014, "reason": "Exact objective quantities were unavailable in the retrieved individual pages; no guessed playable definitions are generated.", "missions": missing}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Catalog: {len(catalog)} definitions; revision coverage: {len(inventory['missions']) - len(missing)}/{len(inventory['missions'])} rows")


def restriction_text(attributes):
    parts = []
    if attributes.get("timeLimitSeconds"):
        parts.append("prazo: " + str(attributes["timeLimitSeconds"]) + " segundos")
    if attributes.get("forbidCloak"):
        parts.append("sem camuflagem")
    if attributes.get("forbiddenWeapon"):
        parts.append("arma proibida: " + {"ANY": "todas", "LASER": "laser", "ROCKET": "foguetes"}.get(attributes["forbiddenWeapon"], attributes["forbiddenWeapon"]))
    if attributes.get("requiredAmmo"):
        parts.append("munição obrigatória: " + attributes["requiredAmmo"].removeprefix("ammunition_laser_"))
    for key, label in [("maxDeaths", "mortes permitidas"), ("maxKills", "abates permitidos"), ("maxDamageTaken", "limite de dano recebido")]:
        if key in attributes:
            parts.append(label + ": " + str(attributes[key]))
    if attributes.get("daily"):
        parts.append("diária; novo aceite após 00:00 UTC depois de concluída")
    return " / " + "; ".join(parts) if parts else ""


def objective_text(c):
    names = {84: "Streuner", 71: "Lordakia", 72: "Devolarium", 73: "Mordon", 74: "Sibelon", 75: "Saimon", 76: "Sibelonit", 77: "Lordakium", 78: "Kristallin", 79: "Kristallon", 80: "Cubikon", 81: "Protegit", 85: "StreuneR", 23: "Boss Streuner", 24: "Boss Lordakia", 25: "Boss Saimon", 26: "Boss Devolarium", 27: "Boss Sibelonit", 28: "Boss Lordakium", 29: "Boss Kristallin", 31: "Boss Mordon", 34: "Boss StreuneR", 35: "Boss Kristallon", 46: "Boss Sibelon", 478: "Boss Kristallin", 479: "Boss Kristallon"}
    ores = {1: "Prometium", 2: "Endurium", 3: "Terbium", 4: "Prometid", 5: "Duranium"}
    subject = c.get("targetNpcName") or c.get("shipFamily") or names.get(c.get("targetShipId"), "")
    if c.get("targetShipIds"):
        subject = ", ".join(dict.fromkeys(names.get(int(i), i) for i in c["targetShipIds"].split(",")))
    if c.get("targetOre"):
        subject = ores[c["targetOre"]]
    location = c.get("targetMaps") or ("X-" + str(c["mapSector"]) if c.get("mapSector") else "")
    verbs = {"killNpc": "Destrua", "killPlayers": "Destrua naves inimigas", "collectOre": "Colete", "collectCargoOre": "Recolha de cargas", "damageNpc": "Cause dano a", "damagePlayers": "Cause dano a naves inimigas", "damageTaken": "Receba dano", "stayOnMap": "Permaneça no mapa por", "elapsed": "Permaneça online por", "travel": "Percorra", "visitMap": "Visite o mapa", "visitPosition": "Voe até as coordenadas", "visitPortal": "Voe até o portal", "visitHomeBase": "Retorne à base", "stealCargoBox": "Recolha cargas pertencentes a inimigos", "sellOre": "Venda na estação"}
    text = f"{verbs[c['type']]} {subject} ({c['target']})"
    if c.get("sequence"):
        text = f"Etapa {c['sequence']}: " + text
    if c.get("branch") or c.get("alternativeGroup"):
        text = "Alternativa: " + text
    if c.get("x") is not None:
        text += f" {c['x'] // 100}/{c['y'] // 100}"
    if location:
        text += " / " + str(location)
    if c.get("mapOwner"):
        text += " / território " + {1: "MMO", 2: "EIC", 3: "VRU"}[c["mapOwner"]]
    if c.get("mapRelation"):
        text += " / território " + ("inimigo" if c["mapRelation"] == "enemy" else "da sua companhia")
    if c.get("targetFactionId"):
        text += " / alvo " + {1: "MMO", 2: "EIC", 3: "VRU"}[c["targetFactionId"]]
    if c.get("weapon"):
        text += " / " + c["weapon"]
    if c.get("requireCloak") == -1:
        text += " / sem camuflagem"
    if c.get("requireCloak") == 1:
        text += " / camuflado"
    if c.get("maxHpPercent", 100) < 100:
        text += f" / HP abaixo de {c['maxHpPercent']}%"
    if c.get("inRadiation"):
        text += " / na radiação"
    return text


if __name__ == "__main__":
    synchronize()
