# DEV Cheatsheet: 2D RTS (Unity)

Быстрая выжимка для разработки: ключевые разделы, где искать детали. Ссылки указывают на строки в исходных .md.

## Архитектура/Слои/Namespaces
- docs/document-2.md:3 — # 1) Solution / namespaces
- docs/document-2.md:5 — - `Domain/Economy`: разнести потоки «гос-ВВП» и «игрок-ВВП» на отдельные подсистемы (налоги, приоритеты госзаказа, субсидии игроку, проценты по займам, лимиты капекса/опекса). Ввести «контракты» (Build/Research/Quarantine) как сущности с жизненным циклом и штрафами за срыв.
- docs/document-2.md:6 — - `Domain/Infection`: слои данных — «уровень заражения», «плотность населения», «мобильность», «санитарные кордоны», «транспортные коридоры».
- docs/document-2.md:7 — - `Application/Pathfinding`: выделить слой навигационных графов в сетке (округа/регионы/горлышки, chokepoints) для тактики и ГОС-ИИ.
- docs/document-2.md:9 — - `Infrastructure`: абстракция сохранений (json/binary) + версия состояния.
- docs/document-3.md:32 — 4) Если что-то «тикает не там»: проверь порядок групп (`Initialization/Simulation/FixedStepSimulation/Presentation`) и атрибуты систем. citeturn0search13
- docs/document-3.md:41 — 1) **SO не мутируем в рантайме.** На старте делай runtime-копию (POCO) и работай только с ней. Добавь `Assert(!Application.isPlaying || !AssetDatabase.Contains(obj))` в редакторских проверках/в тестах.
- docs/document.md:11 — 1) Структура solution / namespaces

## Экономика/Ресурсы
- docs/document-2.md:5 — - `Domain/Economy`: разнести потоки «гос-ВВП» и «игрок-ВВП» на отдельные подсистемы (налоги, приоритеты госзаказа, субсидии игроку, проценты по займам, лимиты капекса/опекса). Ввести «контракты» (Build/Research/Quarantine) как сущности с жизненным циклом и штрафами за срыв.
- docs/document-2.md:59 — - **GOAP** для плана под выбранную цель (собирать ресурсы → исследовать «Щит-I» → построить 3 турели на дуге X). Классический референс — F.E.A.R. (Jeff Orkin) citeturn1search0.
- docs/document-2.md:105 — — SO-дефы (Units/Buildings/Techs/InfectionParams) → runtime-модели; EconomySystem v1; InfectionSystem v1 (SIR-подобный CA с Moore/Neumann).
- docs/document-3.md:10 — 2) Логи порядка вызовов: в начале каждого тика логируй `TickStart(t)`, затем `Infection/Economy/AI`, в конце — `EventBus.Flush()`. Смотри, что порядок стабилен.
- docs/document-3.md:56 — 3) Логируй причины отказов: `[Economy] ReserveFail: amount, balance, who`.
- docs/document-3.md:84 — 3) Ресурсные предусловия: отдельные ассерт-тесты для Preconditions/Effects.
- docs/document-4.md:3 — ## 1) Ресурсы и базовые правила (MUST)
- docs/document-4.md:4 — 1. Ввести **4 базовых ресурса**:

## Постройки/Строительство/Исследования
- docs/document-2.md:5 — - `Domain/Economy`: разнести потоки «гос-ВВП» и «игрок-ВВП» на отдельные подсистемы (налоги, приоритеты госзаказа, субсидии игроку, проценты по займам, лимиты капекса/опекса). Ввести «контракты» (Build/Research/Quarantine) как сущности с жизненным циклом и штрафами за срыв.
- docs/document-2.md:37 — - Гос-бюджет: приоритеты (оборона/карантины/базовые исследования/логистика), авто-перераспределение по Utility-скорам.
- docs/document-2.md:58 — - **Utility AI**: скоринг целей «сдержать очаг», «закрыть горлышко», «исследовать X», «поддержать игрока» — нормированные функции 0..1.
- docs/document-2.md:59 — - **GOAP** для плана под выбранную цель (собирать ресурсы → исследовать «Щит-I» → построить 3 турели на дуге X). Классический референс — F.E.A.R. (Jeff Orkin) citeturn1search0.
- docs/document-2.md:105 — — SO-дефы (Units/Buildings/Techs/InfectionParams) → runtime-модели; EconomySystem v1; InfectionSystem v1 (SIR-подобный CA с Moore/Neumann).
- docs/document-3.md:82 — 1) Лог плана: `[GOAP] Plan: Gather→Research[Shield-I]→Build[Turret×3]`.
- docs/document-4.md:10 — 3. Для каждого здания задать: **стоимость постройки**, **время строительства**, **штатную потребность в труда-часах**, **потребление/выработку энергии**, **обслуживание**, **прирост/переработку ресурсов**.
- docs/document-4.md:16 — 3. Постройками типа **Призывной пункт/Казармы** конвертировать часть прироста в **военных**:

## Юниты/Бой/Боёвка
- docs/document-2.md:1 — Отличный скелет! Ниже — расширения по каждому пункту: больше механик (геймдизайн) и больше «куда копать» по производительности (Unity/С#). Я пишу краткими блоками «Что добавить» → «Как ускорить», чтобы это сразу можно превращать в задачи.
- docs/document-2.md:24 — - Если решите паузить через `Time.timeScale=0`, используйте `unscaledDeltaTime` для UI/меню и анимаций (Animator в Unscaled Mode) — это каноничный паттерн в Unity citeturn5search2turn5search1.
- docs/document-2.md:28 — - «Каталоги» (SO) для юнитов/зданий/технологий/параметров инфекции; на старте — сборка runtime-объектов (иммутабельных где можно).
- docs/document-2.md:88 — # 9) Путь юнита (Pathfinding) на сетке
- docs/document-2.md:105 — — SO-дефы (Units/Buildings/Techs/InfectionParams) → runtime-модели; EconomySystem v1; InfectionSystem v1 (SIR-подобный CA с Moore/Neumann).
- docs/document-2.md:110 — — Пул объектов для визуальных маркеров/снарядов (Unity `ObjectPool<T>`) citeturn6search6.
- docs/document-3.md:43 — 3) Если используешь `SerializeReference` для полиморфизма — проверь в простом юнит-тесте, что `JsonUtility.ToJson/FromJson` корректно восстанавливает подтипы. citeturn0search15
- docs/document-3.md:54 — 1) Unit-тесты на инварианты кошельков: `TryReserve` не меняет баланс, `Commit` — уменьшает; суммы транзакций сходятся в 0 («деньги не исчезают/не появляются»).

## AI/Поведение/Путь
- docs/document-2.md:5 — - `Domain/Economy`: разнести потоки «гос-ВВП» и «игрок-ВВП» на отдельные подсистемы (налоги, приоритеты госзаказа, субсидии игроку, проценты по займам, лимиты капекса/опекса). Ввести «контракты» (Build/Research/Quarantine) как сущности с жизненным циклом и штрафами за срыв.
- docs/document-2.md:6 — - `Domain/Infection`: слои данных — «уровень заражения», «плотность населения», «мобильность», «санитарные кордоны», «транспортные коридоры».
- docs/document-2.md:7 — - `Application/Pathfinding`: выделить слой навигационных графов в сетке (округа/регионы/горлышки, chokepoints) для тактики и ГОС-ИИ.
- docs/document-2.md:8 — - `AI/Perception`: кеши влияния (influence maps) и карт давления по фронту, чтобы Utility-оценки читали готовые поля.
- docs/document-2.md:58 — - **Utility AI**: скоринг целей «сдержать очаг», «закрыть горлышко», «исследовать X», «поддержать игрока» — нормированные функции 0..1.
- docs/document-2.md:64 — - Тикайте стратегию разреженно (каждые N тиков), тактику — чаще; Utility-оценки разбивайте по кадрам (budgeted AI).
- docs/document-2.md:79 — - Эскалация: MonoBehaviour → Jobs+Burst на горячие циклы → DOTS/Entities на подсистемы «сетка/инфекция/массовая тактика».
- docs/document-2.md:88 — # 9) Путь юнита (Pathfinding) на сетке

## UI/UX/Интерфейс
- docs/document-2.md:5 — - `Domain/Economy`: разнести потоки «гос-ВВП» и «игрок-ВВП» на отдельные подсистемы (налоги, приоритеты госзаказа, субсидии игроку, проценты по займам, лимиты капекса/опекса). Ввести «контракты» (Build/Research/Quarantine) как сущности с жизненным циклом и штрафами за срыв.
- docs/document-2.md:19 — - «Жёсткая пауза»: симуляция стоит, UI/анимации UI — живут на `unscaledDeltaTime`.
- docs/document-2.md:23 — - Собственный `SimulationLoop` с аккумулятором на `Time.unscaledDeltaTime` (чтобы пауза через `timeScale` не ломала таймеры UI). Если перейдёте в Entities — используйте `FixedStepSimulationSystemGroup` для фиксации шага «из коробки» citeturn0search5.
- docs/document-2.md:24 — - Если решите паузить через `Time.timeScale=0`, используйте `unscaledDeltaTime` для UI/меню и анимаций (Animator в Unscaled Mode) — это каноничный паттерн в Unity citeturn5search2turn5search1.
- docs/document-2.md:80 — - Пуллинг для частых объектов (снаряды, эффекты, маркеры UI), особенно если Addressables (медленнее Instantiate, но без лагов на главном потоке) citeturn6search6turn6search0.
- docs/document-2.md:101 — — Папки/пустая сцена/Tilemap; `SimulationLoop` + пауза/скорости; базовые интерфейсы Clock/EventBus.
- docs/document-2.md:105 — — SO-дефы (Units/Buildings/Techs/InfectionParams) → runtime-модели; EconomySystem v1; InfectionSystem v1 (SIR-подобный CA с Moore/Neumann).
- docs/document-2.md:118 — — UI скоростей/оверлеев, горячие профилировочные сценарии (толстая карта/много огня/массовый поиск).

## Сохранения/Данные
- docs/document-2.md:9 — - `Infrastructure`: абстракция сохранений (json/binary) + версия состояния.
- docs/document-2.md:12 — - «Тёплые» модели в рантайме = чистые C# классы, а ScriptableObject — только каталоги/дефы, без мутации на лету (модификации SO в рантайме ведут к путанице; лучше инстансить runtime-копии) citeturn0search7turn0search15.
- docs/document-2.md:29 — - Версионирование сохранений: `SaveVersion`, миграции при загрузке.
- docs/document-2.md:33 — - Для ассетов — Addressables с дисциплиной `Load/Release` (зеркалить выгрузки, иначе «висящие» ассеты в памяти) citeturn2search10.
- docs/document-2.md:68 — # 7) События, команды, сохранения
- docs/document-2.md:70 — - Команды с откатом (строительство, приказ, финансирование) → лог в буфере за тик; сохранение после «безопасных» тик-барьеров.
- docs/document-2.md:86 — - Адресация ассетов: строгий `Load/Release` и групповые политики в Addressables — это дисциплина памяти и времени загрузки citeturn2search16turn2search10.
- docs/document-2.md:106 — — Сохранения: JSON (учесть ограничения `JsonUtility`), версия сейва; e2e-тест: старт → пауза → ×2 → сейв/лоад citeturn2search0.

## Активы/Адресация
- docs/document-2.md:33 — - Для ассетов — Addressables с дисциплиной `Load/Release` (зеркалить выгрузки, иначе «висящие» ассеты в памяти) citeturn2search10.
- docs/document-2.md:75 — - В классическом подходе — кольцевой буфер событий и пакетная обработка/флаш по концу тика.
- docs/document-2.md:80 — - Пуллинг для частых объектов (снаряды, эффекты, маркеры UI), особенно если Addressables (медленнее Instantiate, но без лагов на главном потоке) citeturn6search6turn6search0.
- docs/document-2.md:86 — - Адресация ассетов: строгий `Load/Release` и групповые политики в Addressables — это дисциплина памяти и времени загрузки citeturn2search16turn2search10.
- docs/document-2.md:114 — — Pathfinding: A* → включить JPS; API для пакетного поиска.
- docs/document-2.md:117 — — Разрезание тяжёлых шагов на Jobs + Burst (диффузия, influence); Addressables для тяжёлых спрайтов (строгий `Release`).
- docs/document-2.md:127 — - **Addressables**: «зеркальте» загрузки выгрузками (иначе память распухает), группируйте по способам загрузки/частоте использования citeturn2search10turn2search16.
- docs/document-3.md:41 — 1) **SO не мутируем в рантайме.** На старте делай runtime-копию (POCO) и работай только с ней. Добавь `Assert(!Application.isPlaying || !AssetDatabase.Contains(obj))` в редакторских проверках/в тестах.

## CI/CD/Процессы
- docs/document-2.md:12 — - «Тёплые» модели в рантайме = чистые C# классы, а ScriptableObject — только каталоги/дефы, без мутации на лету (модификации SO в рантайме ведут к путанице; лучше инстансить runtime-копии) citeturn0search7turn0search15.
- docs/document-2.md:13 — - Для тяжёлых циклов готовьте переход на Jobs + Burst и/или Entities (DOTS) по «горячим» системам (инфекция, массовое сканирование). Используйте `NativeArray` и `[BurstCompile]` для джобов — это базовая дорожка к масштабированию citeturn0search1turn0search6turn6search14.
- docs/document-2.md:14 — - Структурные изменения в ECS (создание/уничтожение сущностей) всегда через `EntityCommandBuffer` и фиксированный «replay» — так вы избегаете множества sync-point’ов в кадре citeturn0search4turn0search8.
- docs/document-2.md:23 — - Собственный `SimulationLoop` с аккумулятором на `Time.unscaledDeltaTime` (чтобы пауза через `timeScale` не ломала таймеры UI). Если перейдёте в Entities — используйте `FixedStepSimulationSystemGroup` для фиксации шага «из коробки» citeturn0search5.
- docs/document-2.md:24 — - Если решите паузить через `Time.timeScale=0`, используйте `unscaledDeltaTime` для UI/меню и анимаций (Animator в Unscaled Mode) — это каноничный паттерн в Unity citeturn5search2turn5search1.
- docs/document-2.md:32 — - Json для сейвов: помните ограничения `JsonUtility` (нет `Dictionary`, только сериализуемые поля; без полиморфизма) — планируйте структуры под эти ограничения или используйте адаптеры/обёртки citeturn2search0turn2search3turn2search6.
- docs/document-2.md:33 — - Для ассетов — Addressables с дисциплиной `Load/Release` (зеркалить выгрузки, иначе «висящие» ассеты в памяти) citeturn2search10.
- docs/document-2.md:46 — - Выберите основу: SIR/SEIR-подобная модель + клеточный автомат (диффузия по соседям Moore/Von Neumann), усиленная влиянием инфраструктуры (кордоны, турели, «санпункты») и транспортом. Это реалистично и объяснимо дизайнерски (SIR как канон) citeturn4search0.


## Add-ons Quick Links
- States/Diplomacy/Rarities/Anomalies/Automation/Eco: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md
- States/Research Automation Plan: docs/ADDON_STATES_RESEARCH_AUTOMATION_PLAN.md

## Add-ons Notes (working)
- States: map-level factions, AI stance (ally/neutral/hostile), progression.
- Diplomacy: relations, treaties; events drive changes; UI: simple list + actions.
- Research Rarities: common/rare/epic tiers; gating via states/progression.
- Anomalies: world events affecting economy/combat; time-limited; spawn rules.
- Automation: macros for routine actions (build/harvest/research queue) with limits.
- Eco: environmental modifiers (pollution/renewables) affecting costs/effects.

## Project Navigator (Quick)

- Core Plans
  - Master Plan: docs/Master Plan.md
  - Near-Term: docs/NEAR_TERM.md
  - Sprint Plan: docs/SPRINT_PLAN.md
  - Tickets: docs/TICKETS.md
  - Decisions: docs/DECISIONS.md
  - Open Questions: docs/OPEN_QUESTIONS.md

- Add-ons
  - States/Diplomacy/Rarities/Anomalies/Automation/Eco: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md
  - Research Automation Plan: docs/ADDON_STATES_RESEARCH_AUTOMATION_PLAN.md

- Code Map (entry points)
  - CompositionRoot: My project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.cs
  - HUD: My project/Assets/Scripts/Presentation/UI/HudController.cs
  - ResearchPanel: My project/Assets/Scripts/Presentation/UI/ResearchPanel.cs
  - SaveSystem: My project/Assets/Scripts/Infrastructure/Persistence/SaveSystem.cs
  - Economy: My project/Assets/Scripts/Domain/Economy/
  - Build (domain): My project/Assets/Scripts/Domain/Build/
  - Research (domain): My project/Assets/Scripts/Domain/Research/

## Controls & Toggles (Playmode)

- Keys (Input System aware)
  - M: +10 Materials
  - F: +5 Food
  - B: Attempt Test Building (cost from Test Building config or default)
  - R: Start Research (first item in Test Research config)
  - C: Complete Research (same item)
- HUD
  - Save / Load buttons (save.json at Application.persistentDataPath)
  - Research: toggle ResearchPanel (right‑top)

## Save/Load Coverage (Current)

- Economy stocks: yes
- Units: positions + active destinations (restored on load)
- Research: planned (S2‑07) — statuses Queued/Done (to implement)
- Buildings/history: planned (S2‑07+)

## Systems Quick Facts

- Economy
  - Types: ResourceType, ResourceAmount
  - State/Tick: EconomyState, EconomyManager
  - Files: My project/Assets/Scripts/Domain/Economy/*

- Build
  - TryPlace: BuildingService.TryPlace(economy, cost) -> BuildResult/shortfall
  - Use‑case: PlaceBuilding
  - Config: BuildingConfig (Cost)

- Research
  - Store: ResearchStore (Get/Set Status)
  - Status: ResearchStatus (Locked/Queued/Done)
  - Start/Complete: StartResearch (Started/AlreadyQueued/AlreadyDone/InsufficientResources), CompleteResearch
  - Config: ResearchConfig (array of ResearchDef{Id,Cost})
  - UI: ResearchPanel (list + Start/Complete), toggle from HUD

- Persistence
  - SaveSystem (JsonUtility): economy + units (pos/dest). Bind units via BindUnitsEx

- Input/UI
  - Hybrid New/Legacy: compile‑time switches; set Active Input Handling = Both
  - UI click filtering: HudController.AddUiRect + IsPointerOverHud

## Add‑ons Pointers (Sections)

- States/Diplomacy/Rarities/Anomalies/Automation/Eco
  - Sections to find: States, Diplomacy, Research Rarities, Anomalies, Automation, Eco
  - Doc: docs/ADDON_STATES_DIPLOMACY_RESEARCH_RARITIES_AUTOMATION.md

- Research Automation Plan
  - Focus: queues, compute units/cores, roles, interfaces
  - Doc: docs/ADDON_STATES_RESEARCH_AUTOMATION_PLAN.md

## Common Lookups

- Where to start a new research from code
  - StartResearch.Execute(id, cost, out shortfall)
- Where to add a new building type
  - Infrastructure/Configs/BuildingConfig.cs + domain data readers later
- Where HUD/UI input is handled
  - HudController.OnGUI + InputController.Update
- How to prevent world clicks on UI
  - HudController.AddUiRect + HudController.IsPointerOverHud

## Glossary (working)

- Queued Research: started, resources paid, awaiting completion
- Shortfall: list of missing ResourceAmount for current action
- CU (Compute Units): automation compute capacity (add‑on)

## Session Snapshot (Working Memory)

- Save/Load Coverage
  - Economy stocks: yes
  - Units: position, destination, faction, health (restored on load)
  - Research: statuses (Queued/Done) saved and restored
  - Buildings/history: not persisted yet (planned)

- UI Surfaces
  - HUD: resources, Save/Load, toggle Research, toggle Dev Actions
  - ResearchPanel: list, rarity hints, Start/Complete per entry (toggle from HUD)
  - ActionsPanel (Dev): spawn Unit/Enemy, add resources, attempt build, clear save (toggle from HUD)

- Input/Controls
  - Keys: M +10 Materials, F +5 Food, B Build, R Start Research, C Complete Research, E Spawn Enemy
  - New Input System primary; Legacy fallback enabled; recommend Player Settings → Active Input Handling = Both

- Combat
  - UnitCombat: auto‑attack nearest enemy; chase until stop‑distance (~0.9×range); attack on cooldown; destroy on death
  - Factions: Player (white), Enemy (red)

- Editor Utilities (Tools → RTS → Setup)
  - Create Unit Prefab; Add Spawner To Scene; Create Building Config Asset; Add Actions Panel To Scene

- Persistence Paths
  - Save file: `UnityEngine.Application.persistentDataPath/save.json`

## Operating Rules (So we don’t forget)

- Commit discipline: commit and push meaningful steps; keep messages scoped (feature/fix/docs)
- Decisions live in `docs/DECISIONS.md`; close items in `docs/OPEN_QUESTIONS.md` as we resolve them
- Use `global::Game.*` inside `CompositionRoot` to avoid the `Game` namespace vs. property collision
- Use `UnityEngine.Application.persistentDataPath` (avoid `Game.Application.*` ambiguity)
- When adding panels, register rectangles via `HudController.AddUiRect` to filter world clicks
- When extending saves:
  - Add fields to `SaveDto`; keep old fields readable; apply safe defaults on load
  - Provide capture/restore bindings in `CompositionRoot` (e.g., BindUnitsEx)

## Next Steps (Sprint 2)

- S2‑09 Pathfinding planning: grid size, neighbor policy (4/8), base costs, heuristic; document in DECISIONS.md
- S2‑10 Persistence tests: README checklist to verify Save/Load for Economy/Units/Research
- Optional polish: HP overlay for units; refine chase/stop; HUD action shortcuts

- Add‑On Guide: docs/ADDON_GUIDE.md
