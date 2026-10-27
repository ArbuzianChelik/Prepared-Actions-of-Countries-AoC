To compile: dotnet build -c Release

# PAoC (Prepared Actions of Countries) — Guide for Scenario Developers

PAoC lets you script actions that countries perform automatically on specific in-game dates. Just drop a `PAoC.txt` file into your scenario folder, next to your `.aoc` save file.

## File location

PAoC looks for your main script in one of two places (checked in this order of preference):

Custom Scenarios/<Your Scenario Name>/PAoC/PAoC.txt
Custom Scenarios/<Your Scenario Name>/PAoC.txt

- If a `PAoC/PAoC.txt` exists, it is used, and **the `PAoC` subfolder becomes the base directory** for everything else described in this guide (Superevents, News, Events, Conditional_events, Key_events, Delay_events, Flags, Sounds).
- If only `PAoC.txt` in the scenario root exists, that one is used instead, and **the scenario root becomes the base directory** for all the same subfolders.
- If both exist, the mod uses the one inside `PAoC/` and logs a warning — remove the old root-level copy to avoid confusion.

Wherever this guide says "`Superevents/`", "`News/`", "`Conditional_events/`", "`Key_events/`", "`Delay_events/`", "`Events/`", "`Flags/`", or "`Sounds/`", it means that folder placed next to whichever `PAoC.txt` is actually being used. It's recommended to keep everything inside a `PAoC/` subfolder to keep your scenario folder tidy:

Custom Scenarios/<Your Scenario Name>/PAoC/PAoC.txt
Custom Scenarios/<Your Scenario Name>/PAoC/Superevents/...
Custom Scenarios/<Your Scenario Name>/PAoC/News/...
Custom Scenarios/<Your Scenario Name>/PAoC/Events/...
Custom Scenarios/<Your Scenario Name>/PAoC/Conditional_events/...
Custom Scenarios/<Your Scenario Name>/PAoC/Key_events/...
Custom Scenarios/<Your Scenario Name>/PAoC/Delay_events/...
Custom Scenarios/<Your Scenario Name>/PAoC/Flags/...
Custom Scenarios/<Your Scenario Name>/PAoC/Sounds/...

## Basic structure

DD/MM/YYYY = {
COUNTRY_ID = {
command = value
command
...
}
COUNTRY_ID = {
...
}
}

- One or more date blocks per file.
- Each date block can contain one or more country blocks.
- Each country block can contain one or more commands.
- Country IDs are the numbers from your scenario's `flagNames.txt`.
- Lines starting with `#` are comments and are ignored.

## Time scale: Days vs Months

Ages of Conflict lets the player choose between two time scales:

- **Days** — the exact day you specify is respected. `24/02/2020` fires on 24 February 2020.
- **Months** — days don't exist at this scale. Any action scheduled for a given month fires as soon as that month begins, regardless of the day you wrote. `24/02/2020` and `01/02/2020` behave identically — both fire at the start of February 2020.

You don't need to do anything differently in your file — PAoC automatically detects the scenario's current time scale and adjusts.

## Commands

| Command | Syntax | Description |
|---|---|---|
| `attack` | `attack = ID` | Declares war on country `ID`. |
| `fight_to_death` | `fight_to_death = ID` | Declares war on country `ID` with no negotiated peace — the war continues until one side is destroyed. |
| `peace` | `peace = ID` | Makes peace with country `ID`, if currently at war. |
| `join_faction` | `join_faction = ID` | Forms or joins an alliance with country `ID`. |
| `leave_faction` | `leave_faction` | Leaves the country's current alliance. No value needed — a country can only be in one alliance at a time. |
| `puppet` | `puppet = ID` | Turns country `ID` into a puppet of this country. |
| `release` | `release = ID` | Releases country `ID` from being this country's puppet. |
| `rename` | `rename = "New Name"` | Renames the country. Value must be in double quotes. |
| `recolor` | `recolor = "RRGGBB"` | Changes the country's color. Value is a 6‑digit hex code in double quotes, **no leading `#`**. |
| `die` | `die` | Destroys the country entirely. No value needed. |
| `annex` | `annex = ID` | Instantly annexes country `ID` into this country (gains all its cities and tiles). This bypasses war and ignores rightful city ownership. |
| `revolt` | `revolt = REVOLTING_ID FROM_ID` | Makes `REVOLTING_ID` rise up against `FROM_ID`. The revolting country must exist as a destroyed nation in the scenario (with its original capital position saved). The revolt spawns in a city of `FROM_ID` that either is the original capital or has the revolting nation as rightful owner; if none exists, a random city is chosen. All tiles that have the revolting nation as rightful owner also switch to it. |
| `rename_faction` | `rename_faction = "New Alliance Name"` | Renames the alliance that this country currently belongs to. No effect if the country is not in an alliance. |
| `recolor_faction` | `recolor_faction = "RRGGBB"` | Changes the color of the alliance that this country currently belongs to. No effect if not in an alliance. |
| `disband_faction` | `disband_faction` | Dissolves the alliance that this country currently belongs to. All members become independent. No effect if not in an alliance. |
| `power` | `power = set N` <br> `power = add N` | Modifies Combat Efficiency. <br> • `set N` — sets the **displayed** CE to `N` (1–6). <br> • `add N` — adds `N` to the **internal** CE (negative values decrease it). Internal CE ranges 0 (strongest) to 11 (weakest). |
| `power_lock` | `power_lock = 0` <br> `power_lock = 1` | Locks (`1`) or unlocks (`0`) the country's CE. When locked, CE cannot change via game events (but PAoC commands can still modify it). |
| `money` | `money = set AMOUNT` <br> `money = add AMOUNT` | Modifies the country's stored money (bonus points). `set` sets it to the exact value, `add` increases or decreases it (negative amounts work). |
| `rename_city` | `rename_city = "OldName" "NewName"` | Renames all cities with the given old name to the new name. Logs a warning if multiple cities match. |
| `ai` | `ai = ID 0/1` (global) <br> `ai = 0/1` (inside a country block) | Disables (`0`) or enables (`1`) AI for the specified country. If used inside a country block with a single value, it applies to that country. |
| `conditional_event` | `conditional_event = "EventName" on/off` <br> `conditional_event = "EventName" 1/0` | Enables or disables a conditional event. Supported modes: `on`, `off`, `enable`, `disable`, `1`, `0`. See Conditional Events section. |
| `key_event` | `key_event = "EventName" on/off` <br> `key_event = "EventName" 1/0` | Enables or disables a key event (a script that runs whenever a specific keyboard key is pressed). Supported modes: `on`, `off`, `enable`, `disable`, `1`, `0`. See Key Events section. |
| `delay_event` | `delay_event = "EventName" on/off` <br> `delay_event = "EventName" 1/0` | Schedules (or cancels) a one-time effect to run a set amount of in-game time from now. Supported modes: `on`, `off`, `enable`, `disable`, `1`, `0`. See Delay Events section. |
| `sound` | `sound = "filename.mp3" [mode]` | Plays a sound file from `Sounds/`. Mode `on` stops other sounds. Supported: `.wav`, `.mp3`, `.ogg`. |
| `move_camera_to_country` | `move_camera_to_country = "CountryName"` | Moves the in‑game camera to the country with the given name. If multiple countries have the same name, a random one is chosen. |
| `move_camera_to_city` | `move_camera_to_city = "CityName"` | Moves the in‑game camera to the city with the given name. If multiple cities have the same name, a random one is chosen. |
| `set_flag` | `set_flag = "filename.png"` (inside a country block) | Changes the country's flag using a PNG file from the `Flags/` folder. The image is automatically resized to 36×24 pixels. |
| `set_flag_to_country` | `set_flag_to_country = "filename.png" ID` | Changes the flag of the country with the given ID using a PNG file from the `Flags/` folder. |
| `event` | `event = "FolderName"` | Shows an interactive event with up to five options. See the Events section below. |
| `superevent` | `superevent = "FolderName"` | Triggers a superevent from the `Superevents` folder. See the Superevents section below. |
| `news` | `news = "FolderName"` | Triggers a news event from the `News` folder. See the News section below. |

> **Note about `power = set`**: In earlier versions (before v1.3 S2), `set` worked with the internal CE value. Now it works with the displayed value (1–6). For example, `power = set 6` makes the country as strong as possible. Old scenarios using `power = set 0` should be updated to `power = set 6` to keep the same effect.

### Commands that need a target ID (one number)
`attack`, `fight_to_death`, `peace`, `join_faction`, `puppet`, `release`, `annex`

### Commands that need two target IDs (two numbers)
`revolt` — expects two country IDs: the revolting nation and the nation it revolts against.

### Commands that need a text value (in double quotes)
`rename`, `recolor`, `rename_faction`, `recolor_faction`, `superevent`, `news`, `conditional_event` (plus mode), `key_event` (plus mode), `delay_event` (plus mode), `sound`, `move_camera_to_country`, `move_camera_to_city`, `set_flag`, `set_flag_to_country`, `event`

### Commands that need two text values (two quoted strings)
`rename_city`

### Commands with no value
`leave_faction`, `die`, `disband_faction`

### Commands that need a mode and an amount
`power`, `money` — use `add` or `set` followed by an integer.

If a command is missing a required value, or references a country ID that doesn't exist in the scenario, PAoC skips it and logs a warning — it won't crash your scenario.

---

## Advanced structure: grouping and nesting

You can group actions to apply them conditionally, randomly, or just to keep your file organised. All blocks can be nested inside each other.

### Country blocks

As shown above, `ID = { ... }` wraps commands that belong to that country. This is the most common block.

### Chance blocks

Use `N% = { ... }` to give a block a percentage chance to execute.

DD/MM/YYYY = {
10 = {
50% = {
rename = "Lucky Nation"
}
}
}

The block runs only if a random roll (0–99) is less than `N`. In this example, there is a 50% chance the country gets renamed.

### Random blocks

Use `random = { ... }` to pick exactly one option among several weighted choices.

random = {
30% = { attack = 20 }
70% = { peace = 20 }
}

Inside a random block, each option is written as `N% = { ... }`, where `N` is the weight (not necessarily a percentage — the total weight is summed and each option is picked proportionally). The parser then executes the chosen block.

### Conditional blocks (if/else)

Use `if = { ... }` to execute actions based on the state of the **current country** (the one whose ID is in the outer block). Conditions are written as simple commands inside the `if` block.

DD/MM/YYYY = {
10 = {
if = {
in_war
then = { rename = "At War!" }
else = { rename = "At Peace" }
}
}
}

**Supported conditions:**

| Condition | Syntax | Description |
|---|---|---|
| `in_war` | `in_war` | True if the country is currently at war with anyone. |
| `in_war_with` | `in_war_with = ID` | True if the country is at war with the country `ID`. |
| `name_is` | `name_is = "Name"` | True if the country's name exactly matches the given string. |
| `is_puppet` | `is_puppet` | True if the country is a puppet of someone. |
| `is_puppet_of` | `is_puppet_of = ID` | True if the country is a puppet of the country `ID`. |
| `is_in_faction` | `is_in_faction` | True if the country is in an alliance. |
| `is_in_faction_with` | `is_in_faction_with = ID` | True if the country is in the same alliance as the country `ID`. |
| `controls_city` | `controls_city = "CityName"` | True if the current country controls a city with the given name. |
| `country_controls_city` | `country_controls_city = ID "CityName"` | True if the country with `ID` controls a city with the given name. |
| `power_is` | `power_is = N` <br> `power_is = N>` <br> `power_is = N<` | True if the country's **displayed** Combat Efficiency equals N (or >= N, or <= N). N is clamped to 1–6. |
| `has_gold` | `has_gold = N` <br> `has_gold = N>` <br> `has_gold = N<` | True if the country's gold equals N (or >= N, or <= N). N can be negative. |
| `country_exist` | `country_exist = ID` <br> `country_exist` | True if the country with `ID` exists (not destroyed). If no ID is given, checks the current country. |
| `destroyed` | `destroyed` <br> `destroyed = ID` | True if the current country (or country with ID) is destroyed. |
| `destroyed_by` | `destroyed_by = ID` | True if the current country was destroyed by country ID. |
| `own_tiles` | `own_tiles = N` <br> `own_tiles = N>` <br> `own_tiles = N<` | True if the country controls exactly N tiles (or >= N, or <= N). |
| `city_rightful_to` | `city_rightful_to = "CityName" ID` | True if the city with the given name has its rightful owner equal to country ID. If multiple cities, checks the first. |
| `revolted` | `revolted` <br> `revolted_from = ID` | `revolted` – true if the country has ever revolted (originId != 0). `revolted_from = ID` – true if it revolted from country ID. |
| `revolt_able` | `revolt_able = "CityName"` | True if the city can revolt (no rightful owner or rightful owner does not control it). |
| `color_is` | `color_is = "RRGGBB"` | True if the country's current color matches the given hex color (the `#` is optional). Uses a small tolerance, so tiny rounding differences still count as a match. |

The `if` block supports **then** and **else** branches based on the number of true/false conditions:

- `then = { ... }` — executed if **all** conditions are true.
- `then N = { ... }` — executed if exactly `N` conditions are true.
- `then N> = { ... }` — executed if `N` or more conditions are true.
- `then N< = { ... }` — executed if `N` or fewer conditions are true.
- `then all = { ... }` — executed if **all** conditions are true (same as plain `then`).

- `else = { ... }` — executed if **all** conditions are false.
- `else N = { ... }` — executed if exactly `N` conditions are false.
- `else N> = { ... }` — executed if `N` or more conditions are false.
- `else N< = { ... }` — executed if `N` or fewer conditions are false.
- `else all = { ... }` — executed if **all** conditions are false (same as plain `else`).

**Example with multiple then/else branches:**

if = {
is_puppet
in_war
then = { rename = "Puppet at War" } # both true
then 1 = { rename = "Exactly one true" } # exactly one condition true
then 1> = { rename = "At least one true" } # one or more true
else = { rename = "Everything false" } # all false
else 1 = { rename = "One condition failed" } # exactly one false
}

**Note:** the negated `not = { ... }` group and per-country condition blocks (described in the [Conditional Events](#conditional-events) section) are **not** supported inside a plain `if` block — they only work inside the `conditions` block of a Conditional Event and the `available` block of an interactive Event option. Inside `if`, you can only list flat conditions directly.

**Note:** `country_exist = ID` and `destroyed = ID` (with an explicit ID) work correctly even when there is no "current country" in context — for example, in a top-level `if` block that isn't nested inside any country `{ ... }` block. As long as you give an explicit ID, these two conditions always check that specific country regardless of context.

## Colored text

Any text field the game actually displays — `TITLE`, `DESCRIPTION`, `QUOTE`, `QUOTE_AUTHOR`, an option's `name`, and button `BUTTON` text — supports simple inline color tags using the `§` character followed by a one-letter code.

- `§<code>` – starts a colored span using the color for `<code>` (see table below).
- `§!` – ends the current colored span, going back to the normal text color. If you never close a span, it automatically closes at the end of that text field (it won't "leak" into the next field).
- An unrecognized code after `§` (e.g. `§Z`) is silently dropped — the `§` disappears and the letter is left as normal text.

**Color codes (case-sensitive!):**

| Code | Color | Code | Color |
|---|---|---|---|
| `W` | white | `b` (lowercase) | black |
| `B` | blue | `g` (lowercase) | grey |
| `R` | red | `S` | silver (#C0C0C0) |
| `G` | green | `O` | orange (#FFA500) |
| `Y` | yellow | `P` | pink (#FF69B4) |
| `V` | violet (#8A2BE2) | `M` | magenta (#FF00FF) |
| `C` | cyan (#00FFFF) | | |

Note that uppercase and lowercase letters can mean different colors — `B` is blue, but `b` is black; `G` is green, but `g` is grey. Double-check the case when typing these.

**Example:**

TITLE = "§RWarning!§! A new threat emerges"
DESCRIPTION = "The §Bnorthern kingdom§! has declared war on §Rus§!."

- `§Rvar:score§!` also works — variables are substituted first, then the color tags are applied, so you can color a dynamic value.
- This works in superevents, news, and interactive events — including option button text — since all of them go through the same text rendering.

## Variables

Variables allow you to store and modify values during the game. They are global and persist across all date blocks, for as long as the current scenario session is running.

**Note:** all variables are reset (cleared) whenever a scenario is loaded — the same as conditional events, key events, and delay events. If you reload the same scenario, or switch to a different one, don't expect a variable's value to carry over; it needs to be set again from your schedule.

### Defining a variable

Use the `var = { ... }` block anywhere inside a date block.

var = {
name = "score"
set = "10"
}

- `name` – variable name (no spaces allowed).
- `set` – sets the variable to a value (number or text).
- `add` / `subtract` – modifies a numeric variable.

### Using variables in conditions

Inside an `if` block, you can check variables:

- `var:name = N>` – true if variable >= N
- `var:name = N<` – true if variable <= N
- `var:name = "text"` – true if variable equals the string

Alternatively, you can write `var:name > N` and `var:name < N` (without `=`).

### Displaying variables in text

Use `var:name` in any text field (title, description, quote, etc.). It will be replaced with the current value when the event is shown.

**Example:**

var = {
name = "year"
set = "2024"
}
01/01/2024 = {
news = "year_event"
}

In `news.txt`:

TITLE = "Year var:year"
DESCRIPTION = "We are in var:year"

## Nesting and combining

All blocks (`country`, `chance`, `random`, `if`, `var`) can be nested arbitrarily. For example:

01/01/2024 = {
10 = {
50% = {
if = {
is_in_faction
then = { leave_faction }
}
}
random = {
20% = { attack = 20 }
80% = { peace = 20 }
}
}
}

This tries a 50% chance to leave the faction if the country is in one, and always picks randomly between war and peace with country 20.

## Everyone block

The `everyone` block executes its content for every alive country in the game. It can be used at the top level of a date block or inside other blocks. Inside the block, commands without an explicit country ID will be applied to each country individually.

**Example:**

05/01/0001 = {
everyone = {
ai = 0 # disables AI for every country
}
}

You can also use conditions inside `everyone`:

everyone = {
if = {
is_puppet
then = { rename = "Puppet State" }
}
}

**Important:** The `everyone` block iterates over all alive countries at the moment of execution. If a country dies during execution (e.g., via `die`), it will not be processed for the remaining actions within that `everyone` block.

## Superevents

Superevents are full‑screen cinematic events that appear over the game with a custom image, sound, title, quote, and an OK button. They are triggered by the `superevent = "FolderName"` command.

### Folder structure

Place your superevent in:

Superevents/<SuperEventName>/

(i.e. a `Superevents` folder next to your `PAoC.txt` — see [File location](#file-location) for where that is.)

Inside that folder, you must have:

- `superevent.txt` — the definition file.
- An image file (e.g., `image.png`) – referenced in the definition.
- (Optional) A sound file – by default `sound.wav`, but you can specify a different filename.

### `superevent.txt` format

The file uses `key = "value"` pairs, one per line.

image = "image.png"
title = "The Title of the Event"
quote = "The dramatic quote or message"
quote_author = "Author Name"
button = "Continue"
sound = "sound.wav"

- `image` – path to the image file (relative to the folder). Must be a PNG or JPG.
- `title` – displayed at the top of the event.
- `quote` – the main body text. If `quote_author` is present, it will be appended on a new line with a dash.
- `quote_author` – displayed below the quote.
- `button` – text on the OK button. If omitted, defaults to "OK".
- `sound` – path to the sound file (relative to the folder). If omitted, tries `sound.wav`; if that doesn't exist, no sound plays.

**Example:**

image = "collapse.png"
title = "The Empire Falls"
quote = "The great empire has crumbled into dust."
quote_author = "Historian"
button = "Witness"
sound = "dramatic.wav"

Then in your `PAoC.txt`:

01/01/2024 = {
superevent = "Collapse"
}

When the date arrives, the superevent will be shown.

**Audio formats:** Superevents now support `.wav`, `.mp3`, and `.ogg`. The mod detects the format from the file extension.

### `when_activated` block (optional)

You can add a `when_activated = { ... }` block to `superevent.txt`. The commands inside this block are executed **immediately when the event window appears**, without waiting for the player to close it.

This is useful for enabling conditional events, changing game state, or triggering other actions at the exact moment the event is shown.

**Example:**

image = "collapse.png"
title = "The Empire Falls"
quote = "The great empire has crumbled into dust."
when_activated = {
conditional_event = "aftermath" on
}

---

## News

News events are similar to superevents but are designed for smaller, less intrusive messages. They have a smaller frame and can execute an effect (scripted actions) after the player closes the window. Triggered by `news = "FolderName"`.

### Folder structure

Place your news event in:

News/<NewsName>/

(next to your `PAoC.txt`, same as `Superevents`.)

Inside that folder, you must have:

- `news.txt` — the definition file.
- An image file (e.g., `image.png`) – referenced in the definition.

### `news.txt` format

The file uses `key = "value"` pairs, and can optionally contain an `effect = { ... }` block with PAoC commands that run after the news is closed.

image = "news_image.png"
title = "Breaking News"
description = "The world watches as events unfold..."
button = "Got it"
effect = {
10 = {
power = add 5
}
}

- `image` – path to the image file (relative to the folder).
- `title` – displayed at the top.
- `description` – the main text.
- `button` – text on the OK button. Defaults to "OK".
- `effect` – a block containing PAoC commands (same syntax as inside a date block). These commands will be executed **after** the player closes the news window. You can put any command here, but note that it runs in the context of the current game state at that moment.

**Example:**

image = "alliance.png"
title = "New Alliance Formed"
description = "The nations have agreed to a historic pact."
button = "Excellent"
effect = {
10 = {
join_faction = 20
}
20 = {
join_faction = 10
}
}

Then in your `PAoC.txt`:

15/06/2024 = {
news = "AllianceFormed"
}

When the date arrives, the news window appears. After the player clicks the button, the `effect` block is executed (countries 10 and 20 join each other's alliance).

**Important:** The `effect` block can contain any valid PAoC commands, including nested blocks, random, conditions, etc. It runs **after** the player closes the window, so it's great for chaining events that depend on player acknowledgment.

### `when_activated` block (optional)

You can also add a `when_activated = { ... }` block to `news.txt`. The commands inside this block are executed **immediately when the news window appears**, before the player clicks the button.

**Example:**

IMAGE = "retreat.png"
TITLE = "Retreat from Kiev"
DESCRIPTION = "Russian forces are retreating from Kiev."
BUTTON = "Continue"
when_activated = {
conditional_event = "kiev_taken" on
}
effect = {
61 = { revolt = 101 61 }
}

---

## Conditional Events

Conditional events are scripts that trigger when certain conditions are met, at any time during the game (not only on specific dates). They are defined in separate files and enabled/disabled via the `conditional_event` command.

### Folder structure

Conditional_events/<event_name>.txt

(next to your `PAoC.txt`.) Every `.txt` file directly inside this folder is loaded automatically when the scenario starts — you don't need to reference the filename anywhere except in the `conditional_event` command, where you use the file name without the `.txt` extension.

### File format

conditions = {
condition1
condition2
...
}
effect = {
commands
}

- `conditions` – block containing one or more conditions (same syntax as in `if` blocks), or per-country blocks (see below).
- `effect` – block containing PAoC commands to execute when all conditions are true.
- `country = ID` (optional) – a top-level, unquoted numeric field placed outside the `conditions` block. If present, the event is only ever checked against this one country, instead of looping through every alive nation. See "Important" below for how this interacts with per-country condition blocks.

**Example (`kiev_taken.txt`):**

conditions = {
country_controls_city = 61 "Kiev"
}
effect = {
news = "rus_kiev_taken"
}

### Condition operators

By default, a conditional event requires **all** conditions to be true. You can change this by adding an operator before the opening brace:

- `conditions = all { ... }` – all conditions must be true (default).
- `conditions = N { ... }` – exactly `N` conditions must be true.
- `conditions = N> { ... }` – at least `N` conditions must be true.
- `conditions = N< { ... }` – at most `N` conditions must be true.

**Example – trigger when at least one of two countries controls Kyiv:**

conditions = 1> {
country_controls_city = 61 "Kiev"
country_controls_city = 105 "Kiev"
}
effect = {
news = "kiev_taken"
}

### Per-country condition blocks

Instead of (or in addition to) the plain `conditions = { ... }` list, you can group conditions under specific country IDs. Each numeric key inside `conditions` is treated as a country ID, and the conditions inside it are checked **against that specific country** rather than whichever country happens to be evaluated:

conditions = {
61 = {
in_war_with = 105
}
105 = {
in_war_with = 61
}
}
effect = {
news = "mutual_war"
}

- When at least one country block is present, the event **ignores** the normal "check every alive country" behaviour described below and instead checks the listed countries directly.
- **All** listed country blocks must fully match (every condition inside that block must be true) for the effect to fire. If any listed country doesn't exist, is destroyed, or fails one of its conditions, the whole event fails to trigger that tick.
- Any conditions written directly inside `conditions = { ... }` (outside of a numeric block) are still evaluated, but they're added on top of **every** country block's own condition count — i.e. they act as shared/global conditions checked once per listed country.
- This is the recommended way to write events that depend on the relationship between two or more specific countries (e.g. "both of these countries are at war with each other", "country A controls a city that used to belong to country B").

### Negated conditions (`not` block)

Inside a `conditions` block (or an `available` block, see [Events](#events-interactive)) you can wrap one or more conditions in `not = { ... }` to require that **none** of them are true:

conditions = {
not = {
in_war_with = 5
in_war_with = 6
}
}
effect = {
news = "finally_at_peace"
}

- The event only triggers when **every** condition inside the `not` block is false — i.e. it behaves as "not A and not B", not "not (A and B)". If you need to check that at least one of several things is false (a "not all" behaviour), you currently need a plain (non-negated) condition list plus a `then N<` / `conditions = N<` style comparison instead — there's no operator suffix for `not` itself, it always means "none of these are true".
- `not` blocks can be nested and combined with regular conditions and per-country blocks in the same `conditions` (or `available`) block.
- Like per-country blocks, `not` blocks only work inside `conditions`/`available` blocks — they are **not** recognized inside a plain `if` block.

### Enabling/disabling conditional events

By default, all conditional events are disabled. Use the `conditional_event` command to enable or disable them.

conditional_event = "event_name" on
conditional_event = "event_name" off
conditional_event = "event_name" enable
conditional_event = "event_name" disable
conditional_event = "event_name" 1
conditional_event = "event_name" 0

You can place this command inside a date block or inside any country block.

**Example:**

24/02/2022 = {
news = "ukr_rus_war_start"
conditional_event = "kiev_taken" on
}

Once enabled, the event will be checked every in‑game day. When all conditions are true, the effect is executed **once**, and the event is automatically disabled (one-time trigger).

**Important:** There are three ways a conditional event can be evaluated, checked in this priority order:

1. **Per-country blocks** (see above) — if `conditions` contains any numeric country-ID blocks, only those specific countries are checked, and all of them must match.
2. **A single target country** — if you gave the event a `country = ID` field (a top-level `country = ID` line in the file, outside of `conditions`), only that one country is checked.
3. **Every alive country** — otherwise, the mod loops through all currently alive nations and checks the conditions against each one. If multiple countries satisfy the conditions, the effect is executed only for the first one found, and the event is disabled.

To check purely global conditions that don't depend on a specific country, use conditions without country-specific IDs (e.g., `country_exist` without an ID). Some conditions still require a country context to be evaluated, so it's recommended to design events with either per-country blocks or a specific target country whenever the condition logic depends on "who".

---

## Key Events

Key events are scripts that trigger whenever the player presses a specific keyboard key, for as long as the event is enabled. Unlike conditional events, they are **not** one-time — the effect runs again every time the key is pressed while the event is active. They're useful for debug tools, cheat/testing keybinds, or player-triggered scripted moments. They are defined in separate files and enabled/disabled via the `key_event` command, exactly like conditional events.

### Folder structure

Key_events/<event_name>.txt

(next to your `PAoC.txt`.) Every `.txt` file directly inside this folder is loaded automatically when the scenario starts.

### File format

key = "KeyName"
effect = {
commands
}

- `key` – the name of the keyboard key that triggers this event, in double quotes. Names come from Unity's Input System `Key` enum — common examples: `"Space"`, `"Enter"`, `"Escape"`, `"Tab"`, `"LeftShift"`, `"A"`–`"Z"`, `"Digit0"`–`"Digit9"`, `"F1"`–`"F12"`, `"UpArrow"`, `"DownArrow"`, `"LeftArrow"`, `"RightArrow"`. The name is matched case-insensitively. If the name doesn't match a known key, the mod logs a warning and the event never triggers.
- `effect` – block containing PAoC commands to execute every time the key is pressed while the event is enabled. There is no `conditions` block for key events — the key press itself is the only trigger.

**Example (`toggle_ai.txt`):**

key = "F5"
effect = {
everyone = {
if = {
country_exist
then = { ai = 0 }
}
}
}

### Enabling/disabling key events

By default, all key events are disabled. Use the `key_event` command to enable or disable them, the same way as `conditional_event`:

key_event = "event_name" on
key_event = "event_name" off
key_event = "event_name" enable
key_event = "event_name" disable
key_event = "event_name" 1
key_event = "event_name" 0

You can place this command inside a date block, inside any country block, or as a `when_activated` command in a superevent/news file.

**Example:**

01/01/2024 = {
key_event = "toggle_ai" on
}

Once enabled, pressing the configured key runs the `effect` block. The event stays enabled and will fire again on every subsequent press, until you disable it with `key_event = "event_name" off` (or the scenario reloads, which clears all key events and requires re-enabling them from the schedule).

**Important:** Key events are checked once per frame while the game window has focus. If you enable a key event and immediately hold down the key at the same in-game moment PAoC processes that date, the press has to happen **after** the event becomes active to be detected — a key held down before enabling won't retroactively trigger it.

---

## Delay Events

Delay events schedule a **one-time** effect to run a set amount of in-game time after they're enabled (optionally with some extra randomness). They're useful for "X days after this happens, do Y" style scripting, or for spreading out effects so they don't all fire on the same date. They are defined in separate files and enabled/disabled via the `delay_event` command.

### Folder structure

Delay_events/<event_name>.txt

(next to your `PAoC.txt`.) Every `.txt` file directly inside this folder is loaded automatically when the scenario starts.

### File format

delay = {
day = 30
month = 0
year = 0
}
random = {
min = { day = 0 month = 0 year = 0 }
max = { day = 10 month = 0 year = 0 }
}
effect = {
commands
}

- `delay` (optional) – the fixed part of the delay, in `day`/`days`, `month`/`months`, `year`/`years` fields (whole numbers, any of the three can be omitted and default to 0).
- `random` (optional) – adds a random extra delay on top of `delay`, re-rolled every time the event is enabled. Two forms are supported:
  - **Min/max form** — nest `min = { ... }` and/or `max = { ... }` inside `random`, each with `day`/`month`/`year` fields, to pick a uniformly random value between `min` and `max` (inclusive) for each unit independently.
  - **Flat form** — if you don't use `min`/`max`, put `day`/`month`/`year` fields directly inside `random`; these are treated as the maximum, with an implied minimum of 0 (e.g. `random = { day = 10 }` means "0 to 10 extra random days").
- `effect` – block containing PAoC commands to execute once the delay has elapsed. This runs once, with no country context (same as a superevent/news effect) — use country blocks inside it if you need to target specific countries.

Day, month, and year delays are all applied together (fixed + random, added independently per unit) on top of the in-game date at the moment the event was enabled.

**Example (`aftermath.txt`):** fires 30–40 days after being enabled, with no month/year component.

delay = {
day = 30
}
random = {
max = { day = 10 }
}
effect = {
news = "aftermath_report"
}

### Enabling/disabling delay events

By default, all delay events are disabled/unscheduled. Use the `delay_event` command to schedule or cancel them:

delay_event = "event_name" on
delay_event = "event_name" off
delay_event = "event_name" enable
delay_event = "event_name" disable
delay_event = "event_name" 1
delay_event = "event_name" 0

You can place this command inside a date block, inside any country block, or as a `when_activated`/`effect` command anywhere else PAoC commands are allowed.

**Example:**

01/01/2024 = {
delay_event = "aftermath" on
}

Enabling the event (`on`/`enable`/`1`) computes the activation date from the **current in-game date** at that moment (fixed delay + a freshly rolled random delay) and schedules it. Disabling it (`off`/`disable`/`0`) before it fires cancels it — the effect never runs. Enabling an already-scheduled event again re-rolls the random component and recalculates the activation date from scratch (it does not stack).

**Important:** Unlike key events, delay events are **one-time** — once the effect runs, the event is automatically un-scheduled, same as conditional events. If you want it to happen again, you (or its own `effect`) need to `delay_event = "name" on` it again. Reloading the scenario clears all scheduled delay events, so make sure your main schedule re-triggers any you need.

This makes it safe to build a repeating/periodic timer: a delay event's own `effect` can re-enable itself (`delay_event = "aftermath" on`) to reschedule the next occurrence. The mod always un-schedules the old occurrence *before* running the effect, so a self-re-enabling effect correctly ends up scheduled again instead of accidentally being cancelled right after it re-enables itself.

---

## Events (Interactive)

Events are interactive windows that let the player choose between up to **five** options. Each option has a name and an effect that runs when the player clicks it. Events are triggered by the `event = "FolderName"` command.

### Folder structure

Events/<EventName>/
event.txt
image.png

(next to your `PAoC.txt`, same as `Superevents`, `News`, `Conditional_events`, `Key_events`, and `Delay_events`.)

### `event.txt` format

IMAGE = "image.png"
TITLE = "The Title"
DESCRIPTION = "The description text"
CLOSE_BUTTON = "on"
OPTIONS = {
option_A = {
name = "Choice A"
effect = {
61 = { power = set 6 }
}
}
option_B = {
name = "Choice B"
effect = {
61 = { power = set 3 }
}
}
option_C = {
name = "Choice C"
effect = {
61 = { power = set 1 }
}
available = all {
61 = {
power_is = 3>
}
}
}
option_D = {
name = "Choice D"
effect = {
61 = { die }
}
}
option_E = {
name = "Choice E"
effect = {
61 = { leave_faction }
}
}
}

- `IMAGE` – path to the image file (relative to the folder).
- `TITLE` – displayed at the top.
- `DESCRIPTION` – the main text.
- `CLOSE_BUTTON` (optional) – whether the player can dismiss the event without choosing an option, via a small close (✕) button in the corner of the panel. Accepts `"on"`, `"1"`, or `"enable"` to show it; anything else (or omitting the field) leaves the default, which is **on**. If you want to force the player to pick one of the options, you'd need `CLOSE_BUTTON = "off"` — closing the event without a choice runs no effect either way.
- `OPTIONS` – block containing up to **five** options. Each key must start with `option_` (case-insensitive) — the suffix after the underscore is just a label for you and can be anything (`option_A`...`option_E`, `option_1`, `option_yes`, etc.), it isn't shown to the player or used by the mod. Each option must have a `name` and an `effect` block; an option missing `name` is skipped with a warning. If more than 5 `option_*` blocks are present, the extras are skipped with a warning ("Too many options in event, max 5").
- `effect` – PAoC commands to execute when that option is chosen.
- `available` (optional) – conditions that control whether this option can be clicked. See below.

**Button layout:** with 4 or 5 options the buttons automatically become smaller (and the option text shrinks) to fit them all in the same panel — you don't need to do anything differently in your file, this is handled automatically based on how many `option_*` blocks you define.

### The `available` block

By default, every option is clickable. Add an `available = OP { ... }` block inside an option to grey it out (and make it non-clickable) when its conditions aren't met — the button still shows, but with an "unavailable" appearance and no click response.

available = all {
condition1
condition2
}

- `OP` (optional, right after `available =`, before the `{`) — same operators as everywhere else in PAoC: `all` (default if omitted — every condition must be true), an exact number (`available = 1 { ... }`, exactly N conditions true), `N>` (at least N true), or `N<` (at most N true).
- Conditions use the same syntax as everywhere else — the same condition list documented in [Conditional blocks](#conditional-blocks-ifelse), including `not = { ... }` groups.
- **Which country each condition checks:**
  - A plain condition with no country prefix (e.g. `power_is = 3>` directly inside `available`) is checked against **whichever country the player currently has selected** on the map.
  - A condition inside a numbered block (e.g. `61 = { power_is = 3> }`) is checked against that specific country, regardless of what the player has selected.
  - A `var:name` condition doesn't need — and shouldn't have — a country prefix; it checks the global variable directly, the same as inside `if`.
  - If the relevant country can't be resolved (nothing selected and no explicit ID, or the ID doesn't exist), that condition simply counts as false.

**Example:** an option only available if the currently-selected country has at least "medium" power (power level 3 or higher):

option_negotiate = {
name = "Negotiate"
effect = {
61 = { peace = 12 }
}
available = all {
power_is = 3>
}
}

**Example:** an option only available if country 61 is destroyed:

option_release_puppet = {
name = "Release as independent"
effect = {
12 = { release = 61 }
}
available = all {
61 = {
destroyed
}
}
}

**Example usage in `PAoC.txt`:**

01/01/2024 = {
event = "my_event"
}

---

## Full example

War example

21/01/2022 = {
61 = {
attack = 50
}
}
A country reshapes itself and picks a side

15/06/2023 = {
50 = {
rename = "Random Republic"
recolor = "0057B7"
join_faction = 10
}
}
End a puppet relationship and make peace

01/01/2024 = {
61 = {
release = 50
peace = 50
}
}
A scripted collapse

01/03/2025 = {
99 = {
die
}
}
Conditional renaming and alliance action

01/06/2025 = {
10 = {
if = {
in_war_with = 20
then = {
rename = "Defender"
join_faction = 30
}
else = {
rename = "Neutral"
leave_faction
}
}
}
}
Random annexation or revolt

01/12/2025 = {
40 = {
random = {
60% = { annex = 41 }
40% = { revolt = 42 40 }
}
}
}
Money and power adjustments

01/01/2026 = {
10 = {
power = add 5
money = set 100
}
}
Superevent and news example

01/01/2027 = {
superevent = "Crisis"
news = "CrisisReport"
}
Everyone block example

05/01/2027 = {
everyone = {
ai = 0
}
}
Conditional event enable

10/01/2027 = {
conditional_event = "my_event" on
}
Variable example

15/01/2027 = {
var = {
name = "score"
add = "10"
}
if = {
var:score > 5
then = { news = "score_high" }
}
}
Interactive event

20/01/2027 = {
event = "choose_side"
}
Key event enable

25/01/2027 = {
key_event = "toggle_ai" on
}
Delay event enable

26/01/2027 = {
delay_event = "aftermath" on
}

## Notes and limitations

- **One action per line is recommended** for readability, but the parser doesn't require it — whitespace and line breaks are flexible. Block braces `{ }` must be correctly balanced.
- **Country IDs**, not names, are used everywhere. Check your scenario's `flagNames.txt` for the ID-to-name mapping.
- **Neighbours requirement**: `attack`, `fight_to_death`, and `annex` only work if the two countries actually share a border on the map at the moment the action fires. If they don't, the command silently does nothing (this is a hard rule enforced by the game itself, not something PAoC can bypass). `annex` also requires that the target country is not a puppet of someone else.
- **Revolt requirements**: the revolting country must be **destroyed** (have `destroyed = true` in the game's internal data) and must have its original capital position stored. If the mod can't find a suitable city to start the revolt, the command is skipped.
- **Faction commands**: `rename_faction`, `recolor_faction`, and `disband_faction` have no effect if the country is not currently in an alliance. The country itself is used only to locate its alliance.
- **Restarting the scenario** (via the in-game pause menu) correctly reloads `PAoC.txt` and re-runs the schedule from scratch — you don't need to restart the whole game.
- **Superevents and News**: These commands are global and can be placed outside any country block. They will trigger regardless of which country is being processed.
- **News effect block**: The effect runs after the player closes the window, which means the game continues in the background while the news is shown. Use this for events that require player acknowledgment before proceeding.
- **Conditional events**: They are checked every day, but only if they are enabled. They are one-time triggers; after the effect runs, the event is automatically disabled. Events using per-country condition blocks are checked as a single unit (all listed countries must match together); events without them are checked against each alive country individually.
- **Key events**: They are checked every frame (not just once per in-game day), but only if they are enabled. Unlike conditional events, they are **not** one-time — they keep firing on every key press until explicitly disabled with `key_event = "name" off`. Reloading the scenario clears which key events are enabled, so make sure your schedule re-enables any key events you need from the start.
- **Delay events**: They are checked every day, but only once scheduled via `delay_event = "name" on`. They are one-time triggers — after the effect runs, the event is automatically un-scheduled, same as conditional events. Any random component is re-rolled fresh each time the event is enabled, and the activation date is computed from the in-game date at the moment of enabling (not from when the file was loaded). Reloading the scenario clears all scheduled delay events.
- **Image formats**: PNG and JPG are supported. For best results, use the exact dimensions expected by the mod's UI (e.g., for news: 498x579 frame, image area 422x152 with offsets; for superevents: 912x579 frame, image area 810x386 with offsets). The mod will stretch or fit the image as needed.
- **Sound formats**: `.wav`, `.mp3`, and `.ogg` are supported. The mod automatically detects the format based on the file extension.
- **Customising the UI**: The mod looks for `images/se_frame.png`, `images/se_button.png`, `images/news_frame.png`, `images/news_button.png`, `images/event_frame.png`, `images/event_button.png`, `images/button_unavailable.png` (used for greyed-out `available` options), and `images/close_button.png` (used for the event's close/✕ button) inside the mod folder. You can replace any of these to change the visual style; any that are missing just fall back to a plain-colored background instead.
- **Interactive event option count**: `event.txt` supports up to 5 `option_*` blocks. Button size and font shrink automatically once you go past 3 options, so the panel stays the same size regardless of whether you use 2 or 5 options.
- **Interactive event availability**: an option's `available` block is re-evaluated every time the event window opens, based on the country the player has selected *at that moment* — it isn't locked in when the event is scheduled.
- **Flags**: Place custom flags (PNG, 36×24 recommended) in `PAoC/Flags/`. The mod will resize them automatically.
- **Sounds**: Place audio files in `PAoC/Sounds/`. The `sound` command plays them.
