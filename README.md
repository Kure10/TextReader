# TextReaderMM

Prohlížeč velmi velkých textových souborů napsaný ve WPF. Návrh cílí na soubor o velikosti
desítek gigabajtů otevřený na stroji s 16 GB RAM a vypnutým pagefilem, takže se soubor
**nikdy nenačítá celý do paměti** — ani při otevření, ani při scrollování.

## Prerekvizity

- Windows 10 nebo novější
- .NET 10 Desktop Runtime (jen pro build závislý na frameworku)
- Pro sestavení: .NET 10 SDK, případně JetBrains Rider nebo Visual Studio 2026

## Sestavení a spuštění

```
dotnet build -c Release
dotnet run --project TextReaderMM -c Release
```

Samostatný jednosouborový build (na cílovém stroji není potřeba runtime):

```
dotnet publish TextReaderMM -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true
```

## Funkce

- **Zdroje dat**: lokální textový soubor, webová adresa (odpověď se streamuje do dočasného
  souboru a zobrazí jako čistý text, tedy i HTML), generovaný náhodný text s různě
  dlouhými řádky
- **Uložit jako**: zkopíruje aktuální dokument na zvolené místo
- **Hledání**: Ctrl+F, F3 a Shift+F3, na konci přeskočí na začátek, nálezy se zvýrazní,
  aktivní oranžově
- **Filtr**: zobrazí jen řádky obsahující hledaný text
- **Navigace**: kolečko myši, Home/End, PageUp/PageDown, šipky, vše plynule animované

## Jak to funguje

### Řídký index řádků

`LineIndexer` jednou projde celý soubor a zaznamená bajtovou pozici každého tisícího řádku
(`LineIndex.LinesPerBlock`). Při tomto průchodu se nic nedekóduje. Pro 50GB soubor
s padesátibajtovými řádky zabere index zhruba 8 MB, zatímco plný index s pozicí každého
řádku by potřeboval kolem 8 GB.

Indexace běží na vlákně na pozadí a dokument je čitelný, ještě než doběhne. Počet řádků
i délka posuvníku postupně narůstají.

### Čtení řádku

`IndexedTextDocument.GetLine(n)` skočí na pozici bloku `n / 1000`, dočte zbývající konce
řádků a řádek dekóduje. Bloky se dekódují vcelku a dva poslední se drží v cache, takže
scrollování stojí jedno čtení z disku na tisíc řádků. Řádky delší než 8 KB se pro
zobrazení ořezávají.

### Kódování

O volbě mezi UTF-8, UTF-16 LE a UTF-16 BE rozhoduje BOM. Bez něj se použije jednoduchá
heuristika nad prvními 4 KB: převaha nulových bajtů na lichých nebo sudých pozicích značí
UTF-16, jinak se předpokládá UTF-8.

Každé kódování má vlastní `ILineBreakScanner`. V UTF-16 má konec řádku podobu `0A 00` (LE)
nebo `00 0A` (BE), takže bajt `0A` leží vždy na sudé (LE) nebo liché (BE) pozici. Scannery
tuhle paritu počítají z absolutní pozice v souboru, díky čemuž nevadí ani hranice mezi
čtecími buffery. Konec řádku `\r\n` se řeší odstraněním koncového `\r` při dekódování.

### Vykreslování

`TextView` dědí z `FrameworkElement` a kreslí v metodě `OnRender`. Nevznikají žádné objekty
pro jednotlivé řádky — vykreslí se jen těch zhruba 50 řádků, které se vejdou na obrazovku,
přímo do kreslicího kontextu. Náklady tedy nezávisí na velikosti dokumentu.

Pozice scrollování je `double` v jednotkách řádků: celá část je první viditelný řádek
a desetinná část je posun v pixelech uvnitř něj. Právě proto je scrollování plynulé
a ne po řádcích. Použití `double` a `long` místo `int` navíc ruší strop 2,1 miliardy řádků,
který by přinesl `ListBox`.

Klávesové skoky ani kolečko myši nenastavují pozici přímo. Nastaví cíl a pohled se k němu
přibližuje v události `CompositionTarget.Rendering` exponenciální křivkou nezávislou na
snímkové frekvenci.

### Hledání

`TextSearcher` hledá vždy jen jeden výskyt počínaje aktuální pozicí (F3 pokračuje za
předchozím nálezem, Shift+F3 jde zpět) a na konci dokumentu přeskočí na začátek. Sestavení
seznamu všech nálezů je záměrně vynechané: v mnohagigabajtovém souboru by takový seznam byl
obrovský, přitom uživatel potřebuje vždy jen další výskyt. Hledání běží na pozadí a nové
hledání to předchozí zruší.

Jediné místo, kde se sbírají čísla řádků, je filtr, a to s limitem 2 000 000 výsledků.
`FilteredTextDocument` pak mapuje zobrazené řádky na zdrojové, aniž by kopíroval text.

## Editace textu: proč tu není

Aplikace je čtečka, ne editor, a je to vědomé rozhodnutí. Editace velkých souborů totiž boří
předpoklad, na kterém stojí celý návrh: že se soubor nemění.

Řídký index říká „řádek 5 000 000 začíná na bajtu 312 480 921“. Smazání jednoho znaku na
začátku souboru posune všechny následující pozice a index je neplatný; jeho přepočítání
znamená projít celý soubor znovu. Stejný problém má samotný zápis, protože do souboru na
disku nelze vložit bajt doprostřed, aniž by se přepsal celý zbytek.

Řešení, které používají skutečné editory, je **piece table**: původní soubor zůstane jen pro
čtení a vedle něj vzniká přidávací buffer v paměti. Dokument je pak popsaný jako posloupnost
kousků, kde každý ukazuje buď do původního souboru, nebo do bufferu. Editace je rozdělení
kousku a vložení nového, tedy operace nezávislá na velikosti souboru; uložení je sekvenční
zápis kousků do nového souboru.

Aby zůstalo rychlé i „skoč na řádek X“, musí kousky být ve vyváženém stromu, kde si každý
uzel drží počet řádků a bajtů ve svém podstromu — vyhledání řádku je pak logaritmické
(rope / piece tree, jak to dělá například VS Code). K tomu patří kurzor, undo/redo a správa
změněných oblastí.

To je rozsahem jádro textového editoru, což je mimo zadání této úlohy, a proto tu editace
není. Architektura jí ale nebrání: stačilo by přidat implementaci `ITextDocument` postavenou
nad piece table; vykreslování, hledání ani filtr by se měnit nemusely.

## Známá omezení

- Vodorovný posuvník si rozsah určuje podle nejširšího právě vykresleného řádku, protože
  šířka celého dokumentu není bez jeho přečtení známá.
- Výběr a kopírování textu nejsou implementované, zadání je nepožaduje.
- Filtrování prochází soubor, takže u mnohagigabajtového dokumentu trvá zhruba tak dlouho
  jako jeden průchod daty.
- Stažené a vygenerované dokumenty leží v `%TEMP%\TextReaderMM` a mažou se při otevření
  dalšího dokumentu nebo při ukončení aplikace.

## Struktura projektu

```
Core/              práce se souborem, indexace, kódování, hledání   (bez WPF)
Core/Interfaces/   ITextDocument, ILineBreakScanner
Controls/          TextView (vykreslování), TextViewer (posuvníky)
ViewModels/        MainViewModel, SearchViewModel, RelayCommand
Views/             dialogy pro URL a generátor
App.xaml.cs        composition root
```
