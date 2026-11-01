# Colonist Recruitment

[日本語](README.md) | **English**

A RimWorld 1.7 mod that recruits new colonists for a cost over time. Harmony is required.

## Recruitment methods

| Method | Cost | Time | Requirement and start |
|---|---:|---|---|
| Caravan | 50 silver | 6–10 days | No research; select a colonist and right-click a visiting trader |
| Envoy | 100 silver | 4–7 days after request | No research; right-click a non-hostile settlement with a caravan carrying silver |
| Radio | 250 silver | 3–5 days | Microelectronics Basics; select at the comms console |
| Orbital | 600 silver | 1–3 days | AI Computer Core; select at the comms console; arrival by drop pod |

These are defaults. Each request produces 0–3 applicants and is not refunded if none arrive. Applicants come from world factions, which determine race and xenotype. With Ideology enabled, applicants adopt the most common ideoligion among living free colonists (ties favor the player's primary ideoligion, which is also the fallback when no eligible colonists remain). Empire applicants require a colonist with at least the Knight title.

Every method opens a start dialog first, showing the cost, timing and the chances of each applicant count.

- **Social skill**: the higher the Social skill of the colonist who starts a drive (the one talking to the trader, the caravan's negotiator, or the one at the comms console), the more applicants are likely. By default, Social 0 gives 25% each for 0–3 applicants and Social 20 gives 5% / 15% / 50% / 30%.
- **Silver premium**: adding +50% / +100% / +200% to the cost makes the best of 2 / 3 / 4 candidates come for each applicant, rated by skills, passions, work they cannot do and health (traits are not rated). The number of applicants does not change.
- **Asking a faction directly**: open a faction's comms dialog and choose "Ask for settlers" to have that faction call for settlers, for the silver of radio or orbital comms plus 10 goodwill (11–13 at times, by the same adjustment the game applies to requesting a trader). The faction must not be hostile, must keep 0 or more goodwill afterwards, and its previous direct request must have its result.

By default, compare and accept or reject applicants within 24 hours of arrival. Automatic joining is also available. The default cooldown for requesting the same settlement again is 15 days. Mod settings control costs, timing, applicant probabilities, the premium rating, simultaneous requests and more. The recruitment tab shows the progress of running drives, which methods work on the current map, which factions can be asked directly, and up to 100 history entries.

## Installation and saves

Download an available package from [Releases](https://github.com/pengin0503/Rimworld-Recruitment/releases), put the mod folder in RimWorld's `Mods` folder, enable it after Harmony and restart. Removing the mod while applicants await a decision loses those applicants. Back up saves before changing mods.
