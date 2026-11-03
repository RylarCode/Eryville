# Naming

The names the player sees, in one place. **This file is the source of truth**: use these names in the game, the docs, the code's player-facing text, and conversation. When a name is added or changed, update this file in the same change.

**Status**
- **Locked**: decided. Use exactly this.
- **Current**: what the game says today, not decided yet. Fine to use until it is reviewed.
- **To decide**: there is a conflict or a placeholder; see "Open decisions" at the end.

Code names (class and field names) do not have to match, but player-facing text must. Where a code name differs, it is noted so the two can be connected.

## The game

| Name | What it is | Status | Notes |
|---|---|---|---|
| Eryville | The game's title (title screen, repository) | To decide | Unity's Product Name is still "City Building Gacha", which is what shows under the phone icon. See decision D1. |

## Currencies and items

| Name | What it is | Status | Code / asset |
|---|---|---|---|
| Gold | Main currency: wages, building costs, buying letters | Current | `ResourceManager.Gold` |
| Wood | Building material | Current | `ResourceManager.Wood` |
| Letters | The recruitment currency (pull at the Tavern). Full form "recruitment letters"; singular "letter" | Current | `ResourceManager.Letters` |
| Wheat | Raw food material (Farm) | Current | `Resources/Items/Wheat` |
| Flour | Made from wheat (Mill) | Current | `Resources/Items/Flour` |
| Bread | Food (Bakery) | Current | `Resources/Items/Bread` |
| Fish | Food (Fishery) | Current | `Resources/Items/Fish` |
| Fruit | Food (Orchard) | Current | `Resources/Items/Fruit` |
| Pie | Food (Kitchen / Bakery) | Current | `Resources/Items/Pie` |
| Grilled Fish | Food (Kitchen) | Current | `Resources/Items/Grilled Fish` |

Item categories (code `ItemCategory`, shown in the Stock tab filter): Currency, Material, Ingredient, Meal. Status: Current.

## Buildings

| Name | What it does | Status | Asset |
|---|---|---|---|
| City Hall | The town's centre | To decide (D3: city or town) | `Buildings/City Hall` |
| Home | Housing (2 residents) | Current | `Buildings/Home` |
| Gold Mine | Makes gold (3 workers) | Current | `Buildings/GoldMine` |
| Lumberyard | Makes wood (3 workers) | Current | `Buildings/Lumberyard` |
| Farm | Grows wheat | Current | `Buildings/Farm` |
| Mill | Mills flour | Current | `Buildings/Mill` |
| Bakery | Bakes bread and pie; also a place to eat (2 seats) | Current | `Buildings/Bakery` |
| Fishery | Catches fish | Current | `Buildings/Fishery` |
| Orchard | Picks fruit | Current | `Buildings/Orchard` |
| Kitchen | Cooks pie and grilled fish | Current | `Buildings/Kitchen` |
| Granary | Stores more food | Current | `Buildings/Granary` |
| Tavern | A place to eat (6 seats); also where characters are recruited | Current | `Buildings/Tavern`, `Gacha/TavernPool` |

Roads: **Dirt Road** (Current). Road tools in the Build menu: "Remove Road" (Current).

Recipes (shown on a building's details, "Making: ..."): Grow Wheat, Mill Flour, Bake Bread, Bake Pie, Catch Fish, Grill Fish, Pick Fruit. Status: Current.

## Characters

| Name | Who | Status | Asset | Notes |
|---|---|---|---|---|
| Darkie | Common worker, Max Owned 10 | **To decide (D2)** | `Characters/01` | "Darkie" is a racial slur in English. Strongly recommend renaming before anyone else sees the game. |
| John | Epic, unique (Max Owned 1), the sample Connection character | To decide (D4) | `Characters/NewCharacter` | Reads as a placeholder |
| Baker | Rare, works best at a bakery | Current | `Characters/Baker` | |
| Fisherman | Rare, works best at a fishery | Current | `Characters/Fisherman` | |
| Farmer | Common, works best at a farm | Current | `Characters/Farmer` | |
| Gold Digger | Planned: the user's first drawn character | Current (planned) | not made yet | |

The word for the people in the town: the game says "characters" in menus and "villagers" in one stock note. See D5.

## Character stats and states

| Name | What it is | Status | Code | Notes |
|---|---|---|---|---|
| Level | Character level | Current | `Level` | Not used for anything yet |
| Loyalty | 0-100, grows slowly (payday), unlocks Connection | Current | `Loyalty` | |
| Mood | 0-100, short-term happiness | Current | `Mood` | |
| Fed | How full a character is, as a % (details panel) | To decide (D6) | `Satiety` | Docs say "satiety", the game says "Fed" |
| Hungry / Well fed | Hunger states | Current | `IsHungry`, `IsWellFed` | |
| Speed | Walking speed (details panel) | Current | `MoveSpeed` | The breakdown text ("base x loyalty x mood x buffs") reads like debug output |
| Wage | Daily cost in gold ("Wage: 10/day") | Current | `DailyWage` | "Unpaid" when wages could not be paid |
| Rarity | Common, Rare, Epic, Legendary | Current | `Rarity` | |
| Specialty | A building type the character is better at ("Specialty: bakery x1.5") | Current | `Specialty` | |
| Self-awareness | Levels gained from duplicate pulls, each with buffs | To decide (D7) | `AwarenessLevel` | Sounds like a code name |
| Bonuses | Buffs earned through a Connection | Current | `ConnectionBuffs` | |
| Favourite food | Eaten first; a meal of it lifts mood more ("likes Bread") | Current | `favouriteFood` | |
| Activities | Idle, Walking, Working, Resting, Eating (Town list) | Current | `Character.Activity` | Code says "Traveling" |
| Job / Working Location / Home | Where a character works and lives | To decide (D8) | `WorkBuilding`, `HomeBuilding` | The details say "Working Location", the Town list says "No job" |

## Recruitment

| Name | What it is | Status | Notes |
|---|---|---|---|
| Recruit | The gacha menu, and the act of pulling | Current | |
| Hire | The old free hire menu (kept for testing) | Current | See D9 |
| Pull x1 / Pull x10 | One pull / ten pulls | Current | |
| Buy letter | Buy one letter with gold | Current | |
| Pity ("Sure: ...") | Guaranteed rarity after N pulls | To decide (D10) | "Sure:" is unclear |
| New recruit / Duplicate | Reveal screen results | Current | |

## Production and food

| Name | What it is | Status |
|---|---|---|
| Stock | The town's items, and the Town menu tab | Current |
| Making / Uses | A building's recipe output and input | Current |
| Producing / Not producing / waiting for X / no room for X | Production status | Current |
| Worker bonus | Output multiplier from workers | Current |
| Seats / Meal quality | Dining places | Current |
| Meal windows | Breakfast / lunch / dinner hours (not named in the UI) | Current |
| "Find a little by themselves" | Foraging when there is no food | Current |

## Time and world

| Name | What it is | Status | Notes |
|---|---|---|---|
| Morning, Afternoon, Evening, Night | Times of day (toasts, schedules) | To decide (D11) | The lighting uses dawn / day / dusk / night |
| Day | One real day (real-time clock) | Current | |
| Payday | When wages are paid and loyalty changes (each morning) | Current | |
| Peace / War | World state (affects mood and rest places) | Current | Only visible in Cheats so far |
| While you were away | The offline report | Current | |

## Story, quests, Connection

| Name | What it is | Status |
|---|---|---|
| Story | The story menu | Current |
| Chapter | One piece of the story | Current |
| What next / Seen | Story menu sections | Current |
| Quests | The quests menu | Current |
| Main quest / Side quest | Quest kinds | Current |
| Accept / Claim | Take an offered quest / collect a reward | Current |
| Connection | A character's own stories and quests (button and menu) | **Locked** |
| Talk / Next / Needs / Done / Read again | Connection menu rows | Current |
| "has something to share" | Toast when a Connection chapter unlocks | Current |
| Skip | Leave a replayed chapter | Current |

## Menus and buttons

| Name | Where | Status |
|---|---|---|
| Build | Bottom bar: buildings and roads | Current |
| Hire | Bottom bar (testing) | Current |
| Town | Bottom bar: everything you own (tabs: Characters, Buildings, Stock) | To decide (D3) |
| Recruit | Bottom bar: gacha | Current |
| Story | Bottom bar | Current |
| Quests | Bottom bar ("Quests (2)" when something waits) | Current |
| Menu | Top right: game menu (Save, Load, Quit, Notifications, Cheats) | Current |
| View: 3/4 / View: Flat | Top right: squash buildings flat | Current |
| Notifications | Game menu: message history | Current |
| Assign / Move / Connection | Details panel buttons | Current |
| Place / Cancel / Done / Move here | Placing bar on phones | Current |
| Sort / Filter | Town menu | Current |
| Cheats | Game menu (editor and development builds only) | Current (dev only) |

## Open decisions

| # | Question | Options / recommendation |
|---|---|---|
| D1 | The game's name everywhere: "Eryville" (title screen) or something else; and the phone's product name | Recommend "Eryville" everywhere |
| D2 | Rename **Darkie** | Recommend a new name now (any name; it is a common worker) |
| D3 | City or town? "City Hall", product name "City Building Gacha", but the menu is "Town" | Pick one word: "Town Hall" + "Town", or "City Hall" + "City" |
| D4 | **John**: keep or give a proper name | |
| D5 | The word for the people: characters, villagers, residents, townsfolk | Recommend one word in all player text; "residents" / "workers" stay for the roles |
| D6 | "Fed" or "Satiety" (or "Hunger" counting the other way) | Recommend "Fed" in the UI, "satiety" only in code |
| D7 | "Self-awareness" (duplicate pull levels) | Something like "Awakening", "Rank", "Stars", "Bond" (not "Connection") |
| D8 | "Job", "Workplace" or "Working Location" | Recommend "Job" (details: "Job: Gold Mine") |
| D9 | "Hire" vs "Recruit" | Keep both for now (Hire is for testing and may go away) |
| D10 | Pity wording ("Sure: Rare in 3") | e.g. "Guaranteed Rare in 3 pulls" |
| D11 | Times of day: Morning / Afternoon / Evening / Night, or Dawn / Day / Dusk / Night | Pick one set for the UI |
