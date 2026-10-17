# Production: items, recipes, stock

Everything the town makes is data. The Gold Mine and Lumberyard you started with are just buildings with a recipe that has no inputs; farms, mills and bakeries are the same thing with inputs. A new production path (clay -> bricks, logs -> planks, fish -> smoked fish) is assets only, no code.

## The pieces

| Piece | What it is | Where |
|---|---|---|
| **Item** | a kind of thing the town stores: gold, wood, wheat, flour, bread... name, picture, storage limit, edible or not | `Resources/Items/*.asset` (Create > Scriptable Objects > Item) |
| **Recipe** | "uses these, makes those, every N seconds" | `ScriptableObjects/Recipes/*.asset` (Create > Scriptable Objects > Recipe) |
| **Building** | has one or more recipes (and optionally extra storage room) | the building type's *Recipes* and *Storage Bonus* |
| **Stock** | the town's amounts of every item (saved with the other resources) | `ResourceManager` |

Items must be in a `Resources/Items` folder so the game finds them. Gold, Wood and Letters always exist (built in), so older projects keep working.

Included now: items Wheat, Flour, Bread, Fish; recipes Grow Wheat, Mill Flour, Bake Bread, Catch Fish; buildings Farm, Mill, Bakery, Fishery (they use placeholder pictures; replace them with the New Building wizard or by editing the assets).

## How a building makes things

Every second (and the same way while the game is closed) each producing building:
1. needs a worker on site if it has worker slots (a building with none just runs); every worker's productivity makes cycles come faster;
2. needs enough of its **inputs** in the stock, otherwise it runs at the share it can: *Waiting for Wheat*;
3. needs **room** for what it makes, otherwise it makes only what fits: *Storage full: Bread*.

When several buildings want the same input and there is not enough, they all slow down in proportion (nobody is served first). Things made in one step can be used from the next, so chains (farm -> mill -> bakery) work, one link after another.

## Making something new

1. **Item**: Create > Scriptable Objects > Item in a Resources/Items folder. Name, category, a *capacity* (how many the town can hold; 0 = no limit), and for food: *edible* and *nourishment*. No picture needed yet: a coloured square is shown.
2. **Recipe**: Create > Scriptable Objects > Recipe. Add inputs and outputs (item + amount) and the cycle seconds (how long one cycle takes with one ordinary worker).
3. **Building**: Eryville > New Building... (copy settings from the Farm to make another producer), then put your recipe in its *Recipes* list. Several recipes: the first is made unless another is chosen (the picker is still to come; for now it is the *active recipe index*, saved with the game).
4. **Storage**: a Granary is a building with *Storage Bonus* = Wheat 500, Bread 300... its room is added to the item's own capacity while it stands.
5. Run **Eryville > Check Buildings**: it flags broken recipes (no item, 0 amount, nothing made) and items that something uses but nothing makes.

The old *Gold/Wood Generation Per Sec* fields still work (they become a recipe with no inputs), so nothing you built has to change.

## Seeing it

- **Town > Stock** tab: every item with amount / limit and the net change per minute (FULL / EMPTY flags), then a line for each building that is held back and why. Sort by name, amount or net rate (running short first); filter Food or Materials.
- **Building details**: what it makes and uses per second (or per minute for slow things), and why it is not working.
- **Top bar**: gold, wood, letters, and each other item once the town has some (with its limit).
- **Cheats**: +100 of every item, empty every item (to see shortages).

## While the game is closed

Away time (up to **24 hours**, one login a day fills it) is not an estimate with a flat percentage: it is the same production played through in slices, with every worker where their *schedule* says they would be at that real time of day (at work in the work hours, home otherwise). So a building runs only when its workers would be working, shortages and full storage play out as they would have, and a balanced town stays balanced. Coming back shows a line like *Away 8 h: +120 gold, +300 bread. Bakery waited for Flour 40% of the time.*
Short absences use finer slices (about 180 of them, at least 5 s), so a chain is not delayed by more than about 1%.

## Pacing notes

The recipe numbers are placeholders sized so one farm, mill and bakery (two workers each) feed a few dozen characters. Tune them in the assets; nothing else needs to change. Gold and wood are still large numbers per second, as they always were.

## Coming next (food system, phase 2+)

Characters get a hunger meter (100 to 0 over 24 hours), eat in meal windows from this stock (favourite food first), and hunger only dips mood and productivity a little. Edible items and their *nourishment* are already in place for that.
