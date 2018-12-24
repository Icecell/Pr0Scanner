# Pr0Scanner
Dieses Tool durchsucht die Bilder von https://pr0gramm.com/ nach Zahlen (zum Beispiel "10.00 €") und berechnet daraus die Summe. Ebenfalls werden alle Bilder auf einer Oberfläche angezeigt. Grün bedeutet, es wurde eine Zahl gefunden, bei rot nicht.

Ein Linksklick auf das Bild öffnet die Quelle im Browser. Ein Rechtsklick löscht das Bild von der Oberfläche/Berechnung.

Über die Datei "settings.json" kann man Einstellungen vornehmen, wie z.B. nach bestimmten "Tags" suchen.
Die Texterkennung ist sehr rechenintensiv, deshalb laufen in der Standardeinstellung 4 Threads.
Falls die Datei nicht vorhanden ist, wird sie beim ersten Start (Start-Button drücken) erstellt.

Unter [Pr0ScannerWpf/Release1.0.zip](Pr0ScannerWpf/Release1.0.zip) befindet sich eine startbare Exe-Datei.

Die Texterkennung erfolgt mit Hilfe von [Tesseract](https://de.wikipedia.org/wiki/Tesseract_(Software)).

Ps: Erwartet nicht die höchste Codequalität, dies ist mein erstes richtiges C#-Programm.