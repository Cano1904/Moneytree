#!/usr/bin/env python3
"""Erzeugt die Erzählerstimme für Intro, Abspann und die Zeilen im Spiel (game_01 … game_21) mit ElevenLabs (Text-to-Speech-API).

Voraussetzungen (in den Umgebungseinstellungen, NICHT im Chat oder im Code):
  ELEVENLABS_API_KEY   API-Schlüssel (ElevenLabs → Profil → API Keys)
  ELEVENLABS_VOICE_ID  optional: ID der gewählten Stimme (Voice Library → Stimme → „ID kopieren“).
                       Fehlt sie, listet das Skript die verfügbaren Stimmen und bricht ab.
  ELEVENLABS_MODEL     optional: Modell, Standard „eleven_multilingual_v2“ (stabil; „eleven_v3“ für Audio-Tags)
Netzwerk: api.elevenlabs.io muss erreichbar sein.

Aufruf:
  python3 Tools/Voice/generate_elevenlabs.py            # alle Zeilen erzeugen (vorhandene überspringen)
  python3 Tools/Voice/generate_elevenlabs.py --force    # alles neu erzeugen
  python3 Tools/Voice/generate_elevenlabs.py --voices   # Stimmen auflisten
  python3 Tools/Voice/generate_elevenlabs.py intro_01   # nur bestimmte Zeilen
  python3 Tools/Voice/generate_elevenlabs.py --game     # nur die Zeilen im Spiel (game_*)

Ausgabe: RePlanet/Assets/RePlanet/Resources/Voice/<name>.mp3 und eine Längenprüfung gegen die maximale Dauer.
"""
import json, os, subprocess, sys, urllib.request, urllib.error

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "RePlanet", "Assets", "RePlanet", "Resources", "Voice")
API = "https://api.elevenlabs.io/v1"

# (Datei, maximale Dauer in s, Text v3 mit Tags, Text ohne Tags) – identisch zu docs/SPRECHERTEXT_ELEVENLABS.md
LINES = [
    ("intro_01", 5.0, "[calm] Es gab einmal eine Welt, die alles hatte.", "Es gab einmal eine Welt, die alles hatte."),
    ("intro_02", 5.5, "Und alles, was sie hatte … [pause] warf sie fort.", "Und alles, was sie hatte … warf sie fort."),
    ("intro_03", 6.5, "Konsuma versprach uns das Glück. [ironic] Alles. Sofort. Immer neu.", "Konsuma versprach uns das Glück. … Alles. Sofort. Immer neu."),
    ("intro_04", 6.0, "Wir kauften und kauften … [slowly] bis der Müll unsere Städte überragte.", "Wir kauften und kauften … bis der Müll unsere Städte überragte."),
    ("intro_05", 6.0, "Dann bauten wir Archen. [skeptical] „Nur für fünf Jahre“, sagten sie.", "Dann bauten wir Archen. „Nur für fünf Jahre“, sagten sie."),
    ("intro_06", 6.5, "[sad] Zurück blieben die Maschinen. Auf vier Welten. Um aufzuräumen.", "Zurück blieben die Maschinen. Auf vier Welten. Um aufzuräumen."),
    ("intro_07", 5.0, "[quietly] Aus fünf Jahren … wurden fünfzig.", "Aus fünf Jahren … wurden fünfzig."),
    ("intro_08", 5.5, "Eine Maschine nach der anderen … [whispers] verstummte.", "Eine Maschine nach der anderen … verstummte."),
    ("intro_09", 5.5, "Nur eine nicht. [warmly] Eine kleine, sture Maschine.", "Nur eine nicht. … Eine kleine, sture Maschine."),
    ("intro_10", 6.0, "[warmly] Miko. Jeden Morgen. Würfel für Würfel.", "Miko. Jeden Morgen. Würfel für Würfel."),
    ("intro_11", 5.5, "Bis Miko eines Tages etwas fand, das längst verloren war.", "Bis Miko eines Tages etwas fand, das längst verloren war."),
    ("intro_12", 4.5, "[in awe] Einen Keimling. Klein. Grün. Lebendig.", "Einen Keimling. … Klein. Grün. Lebendig."),
    ("intro_13", 4.0, "[firmly] Ein altes Signal erwachte: Programm Zweite Chance.", "Ein altes Signal erwachte: Programm Zweite Chance."),
    ("intro_14", 5.5, "[hopeful] Wenn das Leben zurückkehrt … kehren auch wir zurück.", "Wenn das Leben zurückkehrt … kehren auch wir zurück."),
    ("ending_01", 5.0, "[softly] Und eines Abends leuchteten neue Lichter am Himmel.", "Und eines Abends leuchteten neue Lichter am Himmel."),
    ("ending_02", 4.5, "[relieved] Die Arche Horizont … kam nach Hause.", "Die Arche Horizont … kam nach Hause."),
    ("ending_03", 5.0, "Vier Welten. [proudly] Vier zweite Chancen.", "Vier Welten. Vier zweite Chancen."),
    ("ending_04", 3.5, "[moved] Danke, Miko.", "Danke, Miko."),
    ("ending_05", 6.5, "Was wir fortgeworfen hatten … [softly] hast du uns zurückgegeben.", "Was wir fortgeworfen hatten … hast du uns zurückgegeben."),
    # Im Spiel (Erzähler Helmut, je Spielstand einmal) – Anlässe und Untertitel: Core/Sim/Story.cs
    ("game_01", 5.5, "[calm] Die alte Erde. [pause] Sie hat lange auf jemanden gewartet, der bleibt.", "Die alte Erde. Sie hat lange auf jemanden gewartet, der bleibt."),  # Erste Landung auf TERRA
    ("game_02", 5.5, "Pyra glühte einst vor Arbeit. [sad] Jetzt glüht nur noch der Sand.", "Pyra glühte einst vor Arbeit. Jetzt glüht nur noch der Sand."),  # Erste Landung auf PYRA
    ("game_03", 5.0, "[softly] Pelagia. Ein Meer, das sich nach klarem Wasser sehnt.", "Pelagia. Ein Meer, das sich nach klarem Wasser sehnt."),  # Erste Landung auf PELAGIA
    ("game_04", 6.5, "[quietly] Nivalis. Unter dem Eis schlafen die Server, die uns die Rückkehr versprachen.", "Nivalis. Unter dem Eis schlafen die Server, die uns die Rückkehr versprachen."),  # Erste Landung auf NIVALIS
    ("game_05", 5.0, "[warmly] Das erste Stück ist heimgebracht. So fängt jede Heimkehr an.", "Das erste Stück ist heimgebracht. So fängt jede Heimkehr an."),  # Erstes Mal Müll ins Lager gebracht
    ("game_06", 4.5, "Aus dem, was wir fortwarfen, [warmly] wird wieder etwas wert.", "Aus dem, was wir fortwarfen, wird wieder etwas wert."),  # Erster Verkauf
    ("game_07", 5.0, "Von fern kommt ein Frachter. [warmly] Du bist nicht mehr ganz allein.", "Von fern kommt ein Frachter. Du bist nicht mehr ganz allein."),  # Erste Schrottlieferung per Frachter
    ("game_08", 5.0, "[softly] Ein kleiner Platz, wieder sauber. Das Licht erinnert sich daran.", "Ein kleiner Platz, wieder sauber. Das Licht erinnert sich daran."),  # Erster Lichtpunkt sauber
    ("game_09", 6.5, "Der größte Berg ist abgetragen. [in awe] Darunter liegt eine Straße, die man fast vergessen hatte.", "Der größte Berg ist abgetragen. Darunter liegt eine Straße, die man fast vergessen hatte."),  # Erster Bereich: Hauptmüll entfernt (85 %)
    ("game_10", 6.0, "[quietly] Kein einziges Stück mehr. So sah es hier aus, bevor wir alles fortwarfen.", "Kein einziges Stück mehr. So sah es hier aus, bevor wir alles fortwarfen."),  # Erster Bereich zu 100 % gereinigt
    ("game_11", 5.0, "[in awe] Die Lichter gehen wieder an. [softly] Leise, eines nach dem anderen.", "Die Lichter gehen wieder an. Leise, eines nach dem anderen."),  # Erstes Projekt fertig – die Stadt erwacht
    ("game_12", 5.5, "[moved] Diese Welt atmet wieder. Du hast ihr die zweite Chance gegeben.", "Diese Welt atmet wieder. Du hast ihr die zweite Chance gegeben."),  # Erster Planet komplett (Großprojekt)
    ("game_13", 4.5, "[calm] Ein Sturm zieht auf. Such dir ein Dach, kleiner Freund.", "Ein Sturm zieht auf. Such dir ein Dach, kleiner Freund."),  # Erster Sturm (nicht PYRA)
    ("game_14", 5.0, "Der Sand wandert wieder. [thoughtful] Morgen sehen die Wege anders aus.", "Der Sand wandert wieder. Morgen sehen die Wege anders aus."),  # Erster Sandsturm auf PYRA
    ("game_15", 6.0, "[softly] Die erste Nacht. Auch Maschinen brauchen einen Ort, an dem sie warten können.", "Die erste Nacht. Auch Maschinen brauchen einen Ort, an dem sie warten können."),  # Erste Nacht
    ("game_16", 5.5, "[warmly] Ein neuer Morgen. Die Arbeit ist geduldig – sie hat auf dich gewartet.", "Ein neuer Morgen. Die Arbeit ist geduldig – sie hat auf dich gewartet."),  # Erster Morgen nach dem Schlafen
    ("game_17", 5.0, "[gently] Manchmal geht einem die Kraft aus. Das ist keine Schande.", "Manchmal geht einem die Kraft aus. Das ist keine Schande."),  # Erste Notabschaltung
    ("game_18", 5.5, "[hopeful] Das Eis ruft. Auf Nivalis wartet das letzte Signal.", "Das Eis ruft. Auf Nivalis wartet das letzte Signal."),  # NIVALIS freigeschaltet
    ("game_19", 5.0, "[warmly] Du bist nicht mehr allein. Zu zweit trägt sich jede Last leichter.", "Du bist nicht mehr allein. Zu zweit trägt sich jede Last leichter."),  # Erster Mitspieler im Koop
    ("game_20", 5.0, "Ein Fundstück. [sad] Jemand hat es gewusst – und trotzdem nichts getan.", "Ein Fundstück. Jemand hat es gewusst – und trotzdem nichts getan."),  # Erstes Fundstück
    ("game_21", 6.0, "[in awe] Hier wächst wieder etwas. Ganz von allein, als hätte es nur auf Platz gewartet.", "Hier wächst wieder etwas. Ganz von allein, als hätte es nur auf Platz gewartet."),  # Erste Ökologie wiederhergestellt
]


def request(path, body=None, accept="application/json"):
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        sys.exit("ELEVENLABS_API_KEY fehlt (in den Umgebungseinstellungen hinterlegen).")
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(API + path, data=data, method="POST" if data else "GET",
                                 headers={"xi-api-key": key, "Content-Type": "application/json", "Accept": accept})
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return r.read()
    except urllib.error.HTTPError as e:
        sys.exit("ElevenLabs-Fehler %d bei %s: %s" % (e.code, path, e.read().decode("utf-8", "replace")[:400]))
    except urllib.error.URLError as e:
        sys.exit("Keine Verbindung zu api.elevenlabs.io (%s) – Netzwerkfreigabe prüfen." % e.reason)


def list_voices():
    voices = json.loads(request("/voices"))["voices"]
    for v in voices:
        labels = v.get("labels") or {}
        print("%-24s %-28s %s" % (v["voice_id"], v["name"][:28], ", ".join("%s=%s" % kv for kv in labels.items())))
    return voices


def duration(path):
    try:
        out = subprocess.run(["soxi", "-D", path], capture_output=True, text=True)
        return float(out.stdout.strip())
    except Exception:
        return None


def main(argv):
    force = "--force" in argv
    if "--voices" in argv:
        list_voices(); return
    voice = os.environ.get("ELEVENLABS_VOICE_ID")
    if not voice:
        print("ELEVENLABS_VOICE_ID fehlt. Verfügbare Stimmen:\n")
        list_voices(); sys.exit(1)
    model = os.environ.get("ELEVENLABS_MODEL", "eleven_multilingual_v2")
    use_tags = model.startswith("eleven_v3")
    wanted = [a for a in argv if not a.startswith("--")]
    if "--game" in argv:
        wanted += [n for n, _, _, _ in LINES if n.startswith("game_")]
    os.makedirs(OUT, exist_ok=True)
    report = []
    for name, maxlen, v3, plain in LINES:
        if wanted and name not in wanted:
            continue
        path = os.path.join(OUT, name + ".mp3")
        if os.path.exists(path) and not force:
            report.append((name, maxlen, duration(path), "vorhanden"))
            continue
        body = {
            "text": v3 if use_tags else plain,
            "model_id": model,
            "language_code": "de",
            "voice_settings": {"stability": 0.45, "similarity_boost": 0.75, "style": 0.25, "use_speaker_boost": True, "speed": 0.92},
        }
        audio = request("/text-to-speech/%s?output_format=mp3_44100_192" % voice, body, accept="audio/mpeg")
        with open(path, "wb") as f:
            f.write(audio)
        report.append((name, maxlen, duration(path), "erzeugt"))
        print("✓", name)
    print("\nDatei       max    Länge   Status")
    for name, maxlen, d, st in report:
        warn = "  ZU LANG – neu erzeugen oder schneller" if d and d > maxlen else ""
        print("%-10s %4.1fs  %6s  %s%s" % (name, maxlen, ("%.1fs" % d) if d else "?", st, warn))


if __name__ == "__main__":
    main(sys.argv[1:])
