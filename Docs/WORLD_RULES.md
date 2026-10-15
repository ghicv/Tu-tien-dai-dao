# Tu Tiên Box — World Rule System

## Core model
World State → Rule Evaluation → Action → Consequence → New World State

Rules should create causal chains. Do not author every story directly.

## Rule specification template
For each rule, define:
- Purpose
- Inputs/state read
- Preconditions
- Evaluation cadence
- Action/state changes
- Side effects/events emitted
- Systems affected
- Failure/edge cases
- Tests
- Long-term consequences

## Rule families

### Time
- Simulation tick and time scale.
- Day/night, seasons, years, generations.
- Aging and long-term events.
- Pause and fast-forward.

### Environment
- Terrain and movement constraints.
- Water, climate, fertility, weather.
- Spiritual energy distribution and regeneration.
- Spiritual zones affect cultivation and ecology.

### Life
- Hunger/needs, health, rest, movement.
- Resource seeking and consumption.
- Aging, reproduction, injury, disease if in scope, death.
- Migration under danger or resource scarcity.

### Resources
- Resource generation, growth, harvesting, consumption.
- Regeneration versus depletion.
- Scarcity creates pressure on individuals and organizations.

### Cultivation
- Talent/spiritual root, environment, technique, resources, time, mental state, risk.
- Cultivation gain and bottlenecks.
- Breakthrough requirements and outcomes.
- Realm affects capabilities and lifespan.
- Failure should have defined consequences rather than arbitrary punishment.

### Decisions
NPC decision flow:
Perceive → Evaluate Needs/Goals → Select Feasible Action → Execute → Observe Result.
Use understandable rule-based scoring/goal selection initially; machine learning or LLM-driven NPCs are not required.

### Society and sects
- Groups form when membership, location, resources, and shared goals make sense.
- Organizations manage members, resources, territory, and goals.
- Growth has constraints: resource supply, leadership, competition, internal tension.
- Succession, splitting, decline, and collapse must be possible.

### Conflict
Avoid arbitrary random war. Derive conflict from competition, threats, relationships, power, and resource scarcity.
Conflict consequences may affect population, territory, economy, leadership, and historical events.

### Opportunities and inheritance
Rare locations/items/techniques should have origins, discovery conditions, ownership changes, and possible legacy.
Important inheritance can persist after its original owner dies.

### History
Record meaningful events, not every low-level action each tick.
Store enough causal links to explain important outcomes: who/what, where, when, cause, and consequence.

### Heavenly Dao
Player interventions change world/entity state or simulation rules. The world then reacts through normal systems. Avoid making every intervention a disconnected cheat with no downstream effects.

## Example causal chain
Spiritual energy rises in a region
→ cultivation becomes more attractive
→ cultivators migrate there
→ local resource demand increases
→ groups compete for control
→ a sect forms or conflict starts
→ casualties and inheritance change the future balance of power.

## Rule quality test
A good rule:
- Has a clear cause and effect.
- Interacts with existing systems.
- Can produce different outcomes under different states.
- Can be tested independently.
- Does not require a prewritten story to be interesting.
