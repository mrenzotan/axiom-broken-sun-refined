## **1\. Executive Summary**

### **Game Concept: Axiom of the Broken Sun**

**Axiom of the Broken Sun** is a 2D Pixel Art Platformer RPG that fuses high-stakes exploration with strategic, turn-based combat. Players navigate a world of precise platforming and engage in tactical battles where the primary weapon is the player’s own voice. By speaking real-world chemical reactions into a microphone, players cast "Chemistry Spells," making the mastery of chemical principles the key to progression and survival.

### **Target Audience**

* **Primary:** High school students (approx. ages 12–18) currently studying or preparing for basic chemistry. The game serves as a supplementary learning tool that reinforces classroom concepts through "intrinsic" gameplay.  
* **Secondary:** RPG enthusiasts and "Edutainment" fans (ages 18+) who enjoy high-difficulty platformers and strategic combat systems.

### **Platforms & Tools**

* **Engine:** Unity (2D Suite).  
* **Language:** C\#.  
* **Voice Processing:** **Vosk Library**. Chosen for its ability to provide offline, low-latency speech-to-text, ensuring the voice-activated combat feels responsive and does not require an active internet connection.  
* **Platform:** PC (Windows/Mac/Linux) to ensure ease of access for students and school laboratory environments.

---

## **2\. Narrative & World-Building**

### **2.1 The Premise: What is the overarching conflict?**

	The world has been shattered by the **Cognition Cascade**, a cataclysmic event that unraveled the natural laws of physics and chemistry. The sun is now dim and fractured, and the world consists of floating land-shards drifting above a void. The central conflict involves the protagonist trying to survive and restore balance to this chaotic reality where matter changes unpredictably. Opposing this effort is the **Null-King**, who seeks to accelerate the decay and break all complex matter down into featureless dust.

### **2.2 Characters: Detailed bios for the protagonist and key NPCs.**

**Protagonist: Kaelen (The Law-Breaker)**

* **Background:** Formerly an apothecary’s apprentice in the city of Ferrum, Kaelen was an ordinary boy who spent his life measuring powders and observing chemical changes. He was neither a mage nor a scholar.  
* **Transformation:** During the Cognition Cascade, he was caught in a storm of uncontrolled reactions. Instead of dying, his mind adapted to the chaos, and his left arm crystallized into a **Catalyst Arm**—a living structure of mineral and glass that allows him to guide reactions.  
* **Abilities:** He is an "Alchemical Echo" who perceives the world as elements and formulas. He uses his voice to speak specific commands (e.g., "React: Combust") which his arm executes to impose order on reality.

**Key NPC / Antagonist: The Null-King (The Silent Equation)**

* **Nature:** A being of total decay that represents the absence of structure.  
* **Goal:** To erase the "unstable mixtures" of the world and reduce everything to dust.  
* **Minions:** He commands **Glimmerlings**, creatures born of corrupted materials and unstable mixtures.


### **2.3 Setting**

The game takes place in a post-cataclysmic fantasy world defined by broken physics.

* **Visuals:** The sky is fractured, and the sun is a "broken ember". Landmasses are floating islands drifting in a void.  
* **Environment:** The environment is volatile; water might burn, air might crystallize, and metal might soften without warning.  
* **Locations:** Specific areas mentioned include the ruined apothecary in the city of Ferrum, which is filled with floating bottles and hazardous chemical spills (acids, fuel oils).


### **2.4 Chemistry Integration: How does chemistry exist in this world?**

Chemistry exists as a blend of **real-world scientific principles applied through a magic-like interface**.

* **Scientific Basis:** The world fundamentally operates on real chemical laws. The protagonist must manage actual resources like Water, Fuel, Acid, and Base.  
* **Magical Execution:** Due to the broken laws of reality, "silent intent" no longer works. Chemistry is now integrated through **Voice Activation** and the **Catalyst Arm**. The protagonist casts "spells" by speaking chemical reactions aloud (e.g., "Formulate: Crystal"), using sound to impose pattern and order onto chaos.  
* **System Logic:** In lore terms, Voice acts as the Instruction, the Arm acts as the Execution, and the World is the Reaction Medium.

---

## **3\. Gameplay Mechanics**

### **3.1 Exploration & Platforming (2D Movement)**

While the combat is turn-based, world traversal is real-time and emphasizes precision platforming.

* **Core Movement:** Kaelen features a standard suite of 2D movement: walking, jumping, and dashing.  
* **Environmental Synthesis:** Players use voice commands to manipulate the environment to progress.  
  * **Phase Change:** Speaking **"Formulate: Freeze"** solidifies water into temporary platforms.  
  * **Combustion:** Speaking **"React: Combust"** burns away debris or wooden barriers.  
  * **Precipitation:** Speaking **"Formulate: Crystal"** creates solid bridges across wide gaps.

**3.2 Turn-Based Combat System**

The combat follows a "Reaction Flow" logic, ensuring every action has a visible, scientific consequence.

* **The Turn Cycle:**  
  1. **Kaelen’s Turn:** The player speaks a reaction command (spell).  
  2. **Enemy Turn:** Enemies perform an attack or prepare a behavior (e.g., charging a gas blast).  
  3. **Reaction Phase:** All active chemical reactions resolve. This is when environmental effects or chained reactions (like detonating gas) occur.  
* **Strategic "Break" System:** Similar to *Honkai: Star Rail*, using the correct phase-change or chemical reaction can "Break" a boss, opening a damage window.

### **3.3 Voice-Activated Chemistry (Spell System)**

Commands are categorized by the **Four Core Chemistry Concepts**. The **Catalyst Arm** acts as the executioner for these spoken instructions.

| Category | Spoken Command | Combat/Exploration Effect |
| :---- | :---- | :---- |
| Phase Change | "Formulate: Freeze" | Skips enemy turns or creates ice platforms. |
| Combustion | "React: Combust" | High damage; ignites flammable gas status effects. |
| Acid-Base | "React: Neutralize" | Cleanses player debuffs or removes enemy acid pools. |
| Solubility | "Formulate: Crystal" | Creates temporary cover to reduce incoming damage. |

### **3.4 Resource Management: Material Slots**

Axiom of the Broken Sun replaces traditional "Mana" with **Material Slots**, reinforcing the law of conservation of mass.

* **Reactants:** Kaelen must carry physical reactants: **Water, Fuel, Acid, Base,** and **Mineral Solutions**.  
* **Consumption:** Casting a spell consumes specific combinations of these materials (e.g., Combust consumes Fuel).  
* **Scavenging:** Players must defeat enemies or find environmental "refills" to restock their slots, making resource management a core part of the gameplay loop.

### **3.5 Enemy & Boss Interaction Logic**

Enemies are designed as "Chemical Puzzles" that require specific reactions to defeat:

* **Status Interplay:** A **Gas Bloater** can be detonated early using **Ignite Vapor** for bonus damage before it explodes on the player.  
* **Form Shifting:** A **Meltspawn** in liquid form is immune to physical hits but becomes "Brittle" and takes double damage if the player uses **Freeze**.  
* **Area Denial:** Bosses like the **Corrosion Queen** create "Acid Zones" that deal damage every turn unless the player uses **Neutralize** to clear the arena.

---

## 

## **4\. Game Asset Design**

### **4.1 Visual Style (Pixel Art Resolution)**

The game utilizes a hybrid pixel art resolution to balance production efficiency with visual depth.

* **Environment & Tilesets (16x16):** The world shards and platforms are designed at a lower resolution to evoke a classic retro aesthetic and allow for faster level iteration.  
* **Characters & Bosses (32x32):** **Kaelen** and the various **Glimmerlings** and **Bosses** use a higher resolution. This allows for detailed textures on the **Catalyst Arm** and clearer visual feedback during combat and chemical reactions.

### **4.2 Color Palette: The "Reactive" Palette**

The palette distinguishes the dying world from the vibrant, volatile nature of chemistry.

* **World Base:** Desaturated grays, deep violets, and "void" blacks represent the fractured landscape of the **Cognition Cascade**.  
* **Reaction Highlights:** High-saturation colors are reserved for chemical spells to make them visually distinct:  
  * **Exothermic (Combustion):** Vivid Oranges and Magma Reds.  
  * **Acidic (Corrosion):** Neon "Toxic" Greens and Yellow-Greens.  
  * **Alkaline (Base):** Clean, chalky Whites and Teals.  
  * **Crystalline (Solutions/Precipitation):** Luminescent Blues and Purples for the **Catalyst Arm**.

### **4.3 Sprite Sheets & Animation List**

The following animations are required to support both the real-time platforming and the turn-based combat system.

* **Kaelen (Protagonist) & Glimmerlings:**  
  * **Idle:** A subtle breathing loop; Kaelen’s **Catalyst Arm** should pulse with light.  
  * **Locomotion:** Run (6-8 frames), Jump, and Fall (1-2 frames each).  
  * **Combat:** **Cast** (The specific "Speak" trigger animation), **Basic Attack** (A physical strike), **Hurt** (Recoil frame), and **Die** (Dissolving into elemental dust).  
* **Level Bosses (e.g., Frost-Melt Sentinel, Corrosion Queen):**  
  * **Idle & Cast:** Large-scale loops highlighting their specific chemical affinity.  
  * **Phase-Shift:** A transition animation for bosses that change states (e.g., Ice to Liquid).  
  * **Stagger:** Essential for visualizing the "Break" state after a successful chemical counter.

### **4.4 Audio Design & Voice Feedback**

Since the **Vosk library** is central to the gameplay, audio serves as a critical UX feedback loop.

* **Recognition Cues:** A distinct "Chime" or "Glow" sound when a voice command is successfully recognized.  
* **Failure Cues:** A low-frequency "thud" or "fizzling" sound if a command is not understood or the player lacks the necessary **Material Slots** (Reactants).  
* **Chemical Soundscapes:** Unique SFX for each reaction type—hissing for neutralization, crackling for combustion, and shattering glass for crystallization.

---

## **5\. Technical Architecture**

* **Class Diagrams:** How the Spell System interacts with the Combat Manager.  
* **Database/JSON Structure:** How you store element properties and reaction results.  
* **State Machine:** Documentation for the different game states (Exploration vs. Combat).

---

## **6\. Level Design & Progression**

* **Level Flow:** A map of how levels connect.  
* **Difficulty Curve:** How new chemical concepts are introduced over time.  
* **Tutorialization:** How you teach the player to use their voice without it feeling like a chore.

---

## **7\. User Interface (UI) & UX**

* **HUD:** Health, mana/energy, and the "Microphone Active" indicator.  
* **Menu Navigation:** Keyboard/Controller support for menus.  
* **Accessibility:** Subtitles for voice commands and "text-input" fallbacks for players who cannot use voice.

---

## **8\. Development Roadmap (Thesis Focus)**

* **MVP (Minimum Viable Product):** What is the bare minimum needed for a successful defense?  
* **Bug Tracking:** How you'll handle the inevitable voice-latency issues.

