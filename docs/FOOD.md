# Food: hunger, meals, favourites

Every character has a **satiety** from 100 (full) down to 0 (empty). It drains slowly in real time, characters eat from the town's stock at mealtimes, and being hungry is a gentle nudge, never a punishment. Everything is tuned in one asset: `Resources/FoodSettings`.

## The rules

| | Default |
|---|---|
| Full to empty | **24 real hours** (a character with *appetite* 2 takes 12) |
| Meal windows | breakfast **07-09**, lunch **12-14**, dinner **18-20** (by the real clock, or the in-game clock's hours) |
| Eats in a window if | satiety is **below 65** and it has not eaten in that window yet |
| Eats at any time if | satiety is **below 20** (a snack) |
| A meal restores | about **35** (it eats until it has had that much, or the stock runs out) |
| Hungry | satiety **below 30**: a little less mood each second, and it works up to **30% slower** at the very worst (never below 70% speed) |
| No food in town | each hungry character finds a little by itself, once per meal window (+8), so nobody stays at zero |
| Never | starving to death, leaving, or refusing to work |

So a character eats roughly three meals a day if there is food, and a town with no food at all just gets mildly slower and gloomier.

## What they eat

- Items marked **Edible** with a **Nourishment** value (how much satiety one unit restores). Bread = 5 (a meal is 7 bread), Fish = 6.
- The character's **favourite food** (`favouriteFood` on the character type) is eaten first; a meal that is mostly favourite lifts the mood **+6** instead of +2. Not enough of it? It goes on to other edible food, in name order.
- If several characters are hungry and food is short, **characters who work in food buildings eat first** (they feed the others), and the order rotates so nobody always eats last.

## Seeing it

- A small **picture of the food floats over a character** for a few seconds while it eats (only while it is in view; nothing inside buildings). If the character's animation set has an **eat** animation it plays when the character is standing still: name the frames `eat_down_0`, `eat_down_1`, ... like the others.
- **Character details**: *Fed: 72%* (*hungry* below 30) and *likes Bread*.
- **Town > Characters** has a **Hungry** filter and a "hungry" tag on each hungry row.
- **Town > Stock**: edible items show *eaten today: N*, and notes say how many characters are hungry and whether there is no food at all.
- **Cheats**: everyone full / very hungry, everyone eats a meal now.

## While the game is closed

The same rules are played through with the production: in each slice of the away time everyone gets hungrier and, in meal windows, eats from the (simulated) stock, food workers first. Production uses each worker's hunger (slower when hungry). Coming back, everyone's satiety is where it would have been, the food is gone from the stock, and the report says e.g. *Away 8 h: +120 gold, -14 bread, 2 meals eaten. 1 of 5 characters are hungry.* Mood from meals is not counted while away.

## Making new food and eaters

- **A new food**: an item (see `PRODUCTION.md`) with *Edible* ticked and a *Nourishment*, plus recipes that make it. Characters pick it up automatically.
- **A character who likes it**: `favouriteFood` on the character type (the New Character wizard copies the template's). `appetite` makes them hungrier (big eater) or less so.
- **New game**: *Starter Stock* in FoodSettings (now 40 bread) is what a new game starts with.
- **Pacing**: the numbers in the settings and in the recipes are placeholders. Bread nourishment 5 means one character needs about 20 bread a day; the included Bakery (two workers) makes several hundred.

Eryville > Check Characters warns about an inedible favourite or a zero appetite.

## Not yet (phase 3)

Eating places (Tavern / Bakery seats), "well fed" variety bonuses, more food buildings and the baker / fisherman characters to recruit.
