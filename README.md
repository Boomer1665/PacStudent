# PacStudent

A Pac-Man style game made in Unity. You play PacStudent, a robot vacuum, eating pellets while four pet ghosts chase you around the maze.

There are two levels. **Level 1** is a classic take with hand-written ghost behaviours. **Level 2** swaps the ghosts' brains for a neural network trained with Unity ML-Agents.

## Play

- **Move:** W / A / S / D (PacStudent remembers your last key and turns as soon as that way opens up)
- **Score:** pellet 10, power pellet 50, bonus cherry 100, scared ghost 300
- **Power pellets** scare the ghosts for 10 seconds. Eat them to send them back to the ghost house.
- You have 3 lives. The game ends when you run out or eat every pellet, and your best score and time for each level are saved.

## Running it

1. Install **Unity 6000.4.11f1** and open this folder as a project.
2. The first open takes a while and needs internet, while Unity downloads the packages in `Packages/manifest.json` (URP, 2D packages, ML-Agents).
3. Open `Assets/Scenes/StartScene` and press Play.

## How it works

- **Movement** is grid based: PacStudent and the ghosts lerp from tile to tile at a fixed speed, with no physics movement. Rigidbodies are kinematic and only used for trigger collisions.
- **Level 1 ghosts** each have their own style: one runs away, one closes in, one wanders, and one follows the outer wall. Scared ghosts all flee, and dead ghosts fly straight back to the house.
- **Level 2 ghosts** ask a trained network which way to go at every tile. It sees 49 numbers: where PacStudent is and where it is heading, open directions, maze distances, the other ghosts, and where the pellets are. The ghosts share one brain and were trained as a team with PPO for 3 million steps against a bot that eats pellets and runs from the nearest ghost. A small scripted rule stops neighbouring ghosts picking the same tile.

Against that bot, four trained ghosts caught it in about 4.8 s on average, compared with 5.8 s for four Level 1 style ghosts and 4.2 s for four shortest-path chasers. The goal was ghosts that hunt differently while staying dangerous, not the fastest chaser. These numbers come from testing against a bot, not people.

## Project layout

```
Assets/Scenes/       StartScene, Level01, InnovationScene (Level 2), TrainingScene
Assets/Scripts/      game code
Assets/Scripts/ML/   ghost brain and training code
Assets/ML-Agents/    the trained model (GhostChaser_v2.onnx)
Training/            training config and graphs
PiskelFiles/         sprite source files
```

## Retraining the ghosts (optional)

1. Create a Python 3.10.12 environment and install `mlagents==1.1.0`, plus `setuptools<81` and `torch<2.9` (newer versions break it).
2. In `TrainingScene`, set the ghosts to **Behavior Type: Default** with no model.
3. Run `mlagents-learn Training/ghost_training_v2.yaml --run-id=myrun --results-dir=Training/results`, then press Play in Unity.
4. Copy the exported `.onnx` into `Assets/ML-Agents` and assign it to the ghosts (Inference Only, 49 inputs).

## Credits

- Sprites drawn in Piskel (source files in `PiskelFiles`).
- Music and sound effects from [Pixabay](https://pixabay.com), used under the Pixabay Content License.
