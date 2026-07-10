# How It Works

Every Channel Point redeem follows the same process.

```
Viewer redeems reward
        │
        ▼
Load pokemon.json
        │
        ▼
Load collections.json
        │
        ▼
Roll shiny
        │
        ▼
Roll BST bracket
        │
        ▼
Roll rarity
        │
        ▼
Roll Pokémon type
        │
        ▼
Choose Pokémon
        │
        ▼
Already owned?
        │
   ┌────┴────┐
   │         │
  Yes       No
   │         │
Continue   Save catch
   │         │
   ▼         ▼
Next roll  Reset streak
```

If every attempt results in a duplicate, the player's loss streak increases by one.

Each future loss streak grants one additional encounter attempt.

Collections are stored permanently in collections.json.
