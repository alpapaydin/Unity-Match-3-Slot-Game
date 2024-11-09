# Match-3 Slot Hybrid Game

A unique gaming experience that combines the excitement of slot machines with the strategic gameplay of match-3 puzzles. Built with Unity, this project demonstrates smooth animations, optimized performance, and engaging game mechanics.

![Preview](preview.png)

## 🎮 Game Features

### Core Mechanics
- **Dynamic Board Sizes**: Randomly switches between 5x5 and 7x7 grid configurations
- **Slot Machine Spin**: 
  - Smooth spinning animation with player-controlled stop
  - 7 distinct tile types with minimum 3 tiles of each type
  - Guaranteed no immediate matches after spin
- **Match-3 Gameplay**:
  - Classic swipe mechanics similar to Candy Crush
  - Matches can be made with 3+ tiles in rows or columns
  - Non-matching swipes are preserved for strategic gameplay
  - 1-second animation duration for tile swaps

### Advanced Features
- **Smart Move Counter**: 
  - Calculates minimum moves required for a match after each spin
  - Helps players strategize their approach
- **Queue System**: 
  - Players can queue next move during animations
  - Ensures fluid, uninterrupted gameplay
- **Move Validation**:
  - Real-time checking for available valid moves
  - "No moves available" detection with restart option
- **Victory Celebration**:
  - Engaging congratulations pop-up
  - Automatic game reset for continuous play

## 🛠 Technical Implementation

### Performance Optimization
- Optimized for mid to low-end mobile devices
- Direct Unity rendering (no Canvas package)
- Efficient animation system using tween library
- Dynamic screen resolution support (Target: 2436x1125 portrait)
- Scalable grid system supporting future expansions

### Architecture Highlights
- Clean separation of game logic and presentation
- Event-driven system for game state management
- Optimized tile pooling for memory efficiency
- Smooth animation pipeline with queue management

## 🎯 Getting Started

### Prerequisites
- Unity 2021.3 LTS or later
- Basic understanding of Match-3 and slot machine mechanics

### Installation
1. Clone the repository
2. Open the project in Unity
3. Load the main scene from `Assets/Scenes`
4. Press Play to test in editor

### Building
1. Set up your build settings for Android/iOS
2. Configure the target resolution (2436x1125 portrait)
3. Build and run!

## 🎨 Customization

### Visual Theming
- Easy tile sprite replacement in the `TileConfig` scriptable object
- Configurable animation parameters
- Customizable UI elements and effects

### Gameplay Parameters
- Adjustable board sizes
- Configurable tile types and minimum counts
- Tweakable animation durations
- Customizable victory conditions

## 🔍 Code Structure

```
Assets/
├── Scripts/
│   ├── Core/           # Core game logic
│   ├── UI/             # User interface elements
│   ├── Animation/      # Animation controllers
│   └── Utils/          # Helper utilities
├── Prefabs/            # Reusable game objects
└── Scenes/             # Game scenes
```

## 🎯 Future Improvements

- [ ] Special tile types with unique effects
- [ ] Combo system for advanced scoring
- [ ] Progressive difficulty system
- [ ] Additional victory animations
- [ ] Achievement system

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Acknowledgements

- Inspired by Coin Master and Candy Crush mechanics
- Built for Grand Games technical assessment
- Animation inspiration from "Magic Sort!"

---
Made with ♥️ for Grand Games
