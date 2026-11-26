# Screenshot Trimmer

A desktop app that turns raw screenshot files into clean, publishable images by cropping them to their subject and finishing the edges.

## Language

### Finding the subject

**Subject**:
The window, dialog, menu or button a screenshot is about; a window together with a **Popup overhang** counts as one Subject. In a browser screenshot with no dialog, maximised or not, the Subject is the web page below the **Browser bar**.
_Avoid_: Target, object, element

**Browser bar**:
The browser's tab strip, address bar and any bookmarks bar above the web page in a browser screenshot.
_Avoid_: Browser chrome, toolbar, header

**Popup overhang**:
The part of a menu or popup that extends past the border of the window it belongs to.
_Avoid_: Overflow, spill

**Classic crop**:
A crop to exactly the rectangle the user draws.
_Avoid_: Manual crop, common crop

**Auto crop**:
A crop to the **Subject** the app finds in the whole image, with no input from the user.
_Avoid_: Automatic crop, smart crop

**Auto crop in region**:
A crop to the largest **Subject** that lies fully inside a rough rectangle the user draws.
_Avoid_: Assisted crop, snap crop

**Loupe**:
The floating magnified view of the pixels around the cursor, shown while the user adjusts a **Classic crop**.
_Avoid_: Zoom, magnifier

### Finishing

**Clean edges**:
The two finishing fixes to a **Subject**'s outline: removing leftover frame lines around it, and making the pixels outside its rounded corners transparent so it keeps its rounded shape.
_Avoid_: Trim, clear corners, square off

**Shadow**:
A soft drop shadow drawn around the **Subject**, following its outline, on transparent padding.
_Avoid_: Glow, border

**Step**:
One pending change to an image (a crop, **Clean edges** or **Shadow**) that is previewed but not yet written to disk.
_Avoid_: Operation, edit, action

### Files

**Original**:
The screenshot file as it was when opened, before any **Step** is applied.
_Avoid_: Source, input

**Pasted image**:
An image brought in from the clipboard, which has no **Original** file and no name until the user chooses where to save it.
_Avoid_: Clipboard image, untitled

**Result**:
The image produced by applying the **Steps** to an **Original** or a **Pasted image**; always a PNG.
_Avoid_: Output, processed file

**Suffix mode**:
The save rule where the **Original** is left untouched and the **Result** is saved beside it under the Original's name plus a suffix.

**Backup mode**:
The save rule where the **Result** takes the **Original**'s name and the Original is kept beside it under its name plus a suffix.
_Avoid_: Overwrite mode, in-place mode

**Batch**:
Applying **Auto crop** and the current finishing settings, as one pending **Step**, to every listed image that needs it, each **Result** reviewable before saving. A Batch skips images that already have Steps, images already processed (they already have a **Shadow**'s transparent padding) and listed **Results**, and writes nothing to disk.
_Avoid_: Bulk process, run all

**No Subject found**:
The mark a **Batch** leaves on an image in which it found no **Subject**: the image gets no Step and stays marked until it gets Steps, by hand or from a later Batch.
_Avoid_: Miss, failed, unprocessed

### Bulgarian names

The Bulgarian interface uses exactly these names for the terms above.

| English | Bulgarian |
|---|---|
| **Classic crop** | Класическо изрязване |
| **Auto crop** | Автоматично изрязване |
| **Auto crop in region** | Автоматично изрязване в област |
| **Clean edges** | Изчистване на ръбовете |
| Frame lines | Рамкови линии |
| Rounded corners | Заоблени ъгли |
| **Shadow** | Сянка |
| **Loupe** | Лупа |
| **Original** | Оригинал |
| **Result** | Резултат |
| **Suffix mode** | Режим с наставка |
| **Backup mode** | Режим с резервно копие |
| **Batch** | Пакетна обработка |
| **No Subject found** | Без намерен обект |
