# Installation

This guide takes about **5–10 minutes**.

No coding experience is required. You only need to copy files, create one Streamer.bot action, and connect it to a Twitch Channel Point reward.

---

## Step 1 — Download the project

Download or clone this repository.

You should have these important files:

```text
StreamerBot/CatchAction.cs
Data/pokemon.json
```

---

## Step 2 — Find your Streamer.bot folder

Find the folder containing:

```text
Streamer.bot.exe
```

Example:

```text
C:\Streamer.bot
```

Your actual location may be different.

---

## Step 3 — Create the data folder

Inside your Streamer.bot folder, create a new folder named:

```text
PokemonCatchData
```

It should look like this:

```text
Streamer.bot
├── Streamer.bot.exe
└── PokemonCatchData
```

---

## Step 4 — Add the Pokémon database

From the downloaded project, copy:

```text
Data/pokemon.json
```

into:

```text
PokemonCatchData
```

Your Streamer.bot folder should now look like this:

```text
Streamer.bot
├── Streamer.bot.exe
└── PokemonCatchData
    └── pokemon.json
```

Do not rename `pokemon.json`.

---

## Step 5 — Create the action in Streamer.bot

Open Streamer.bot.

Go to the **Actions** section.

Create a new action named:

```text
Pokémon - Catch
```

Make sure the action is enabled.

---

## Step 6 — Add the C# code

Inside the `Pokémon - Catch` action, add a new sub-action:

```text
Core
→ C#
→ Execute C# Code
```

Open this project file:

```text
StreamerBot/CatchAction.cs
```

Copy all the code from the file.

Paste it into the Streamer.bot C# editor.

Click:

```text
Compile
```

Make sure there are no red errors.

Then click:

```text
Save and Compile
```

---

## Step 7 — Create the Twitch Channel Point reward

Create a Twitch Channel Point reward.

Example name:

```text
Catch a Pokémon
```

You may choose any reward cost you want.

---

## Step 8 — Connect the reward to the action

Open the `Pokémon - Catch` action in Streamer.bot.

Add this trigger:

```text
Twitch
→ Channel Reward
→ Reward Redemption
```

Select your Channel Point reward:

```text
Catch a Pokémon
```

Make sure you use **Reward Redemption**, not **Reward Redemption Updated**.

---

## Step 9 — Test the system

Make sure Streamer.bot is running and connected to your Twitch broadcaster account.

Redeem the Channel Point reward from Twitch.

A successful result should look similar to:

```text
KapteinOle caught Azumarill! 1/1234 Pokémon and 0/1234 shinies.
```

The exact Pokémon and total number may be different.

---

## Step 10 — Confirm the save file was created

After the first successful catch, the system automatically creates:

```text
PokemonCatchData/collections.json
```

It also creates a backup folder:

```text
PokemonCatchData/Backups
```

Your folder should now look like this:

```text
Streamer.bot
├── Streamer.bot.exe
└── PokemonCatchData
    ├── pokemon.json
    ├── collections.json
    └── Backups
```

You do not need to create `collections.json` yourself.

---

## Finished

The system is now ready.

Viewer collections are automatically saved and will still exist after:

- Ending the stream
- Closing OBS
- Closing Streamer.bot
- Restarting the computer

Do not delete `collections.json` unless you want to reset every viewer's collection.
