# WeekTray

Toont het huidige ISO-weeknummer als pictogram in het systeemvak van Windows.

- Beweeg over het pictogram voor een overzicht van de huidige en komende weken met hun datums.
- Het weeknummer werkt zichzelf bij, ook na slaapstand of een gewijzigde klok.
- De kleur volgt het lichte of donkere thema van de taakbalk.
- Rechtsklik op het pictogram voor `Aantal weken vooruit` (4, 8 of 12), `Starten met Windows` en `Afsluiten`.

Windows 11 heeft geen optie om weeknummers in de agenda van de taakbalk te tonen. WeekTray staat daarom los van die agenda en grijpt niet in op Windows Verkenner.

## Installeren

Er is geen SDK nodig; de C#-compiler van Windows zelf wordt gebruikt. Beheerdersrechten zijn niet nodig.

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Het script:

1. bouwt `bin\WeekTray.exe`;
2. kopieert het naar `%LOCALAPPDATA%\Programs\WeekTray`;
3. zet WeekTray in de autostart van de huidige gebruiker;
4. start WeekTray en zet het pictogram vast op de taakbalk.

## Verwijderen

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

## Ontwikkelen

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

Met `-PreviewDir <map>` schrijft de test ook voorbeeldpictogrammen als PNG weg.
