Архитектурный план проекта (C# / Unity) — 2D RTS с паузой, «двумя ВВП» (гос-ИИ и игрок) и симуляцией заражения
==============================================================================================================

Мини-допущения
--------------
- 2D на Tilemap; одна планета = одна большая карта (тайловая сетка).
- Пауза «хардовая» (останавливаем время симуляции), UI и меню — живут.
- «Гос-ИИ» — стратегический слой (макроэкономика/приоритеты), «локальный ИИ» — тактика/оборона/распространение.
- Производительность: старт на MonoBehaviour + ScriptableObject; при росте агентов подключаем Job System/Burst, при необходимости — DOTS/Entities для фиксированного тика.

1) Структура solution / namespaces
----------------------------------
/Game
  /Domain              // Чистые модели (POCO) и интерфейсы
    Economy/           // ВВП, бюджеты, производство, издержки
    Infection/         // Состояние клеток, параметры заражения
    Tech/              // Дерево технологий, эффекты
    Map/               // Координаты, граф смежности, зоны
    AI/                // Контракты AI: IStrategicPlanner, IUtilityScorer...
    Common/            // EventBus, Commands, Result, Time abstractions
  /Data                // ScriptableObject-конфиги (статические данные)
    Units/, Buildings/, Techs/, Factions/, InfectionParams/
  /Application         // Сервисы симуляции, претик/тик, сохранения
    Simulation/, Saving/, Pathfinding/, Services/
  /AI                  // Реализация: Utility AI, GOAP, Behavior Trees
    Strategic/, Tactical/, Perception/
  /Presentation        // MonoBehaviour: сцены, UI, инпут, визуализация
    UI/, VFX/, Audio/, Tilemaps/
  /Infrastructure      // Репозитории данных, JSON, DI/Service Locator

Пояснения:
- ScriptableObject — только «статические каталоги» (юниты, здания, технологии, параметры заражения).
  В рантайме — копии в виде обычных C#-объектов (иммутабельные где возможно), чтобы не мутировать ассеты.
- Tilemap — для сетки и рендеринга 2D-мира.
- EventBus / Command — слабосвязанная коммуникация между подсистемами (симуляция ⇄ UI, AI ⇄ экономика).
- Сохранения — JsonUtility (просто) или System.Text.Json (если нужен внешний формат/библиотеки).

2) Тайминг, пауза, и «фиксированный тик»
----------------------------------------
Цель: симуляция идёт фиксированным шагом (например, 5–10 тиков/сек), независимо от FPS; пауза останавливает симуляцию, но не UI.

Варианты:
- Свой SimulationLoop с накоплением accumulator += unscaledDeltaTime; пауза = isPaused = true (UI живёт).
- Можно использовать Time.timeScale = 0 для глобальной паузы (просто), но лучше контролировать симуляцию явным циклом.
- В DOTS/Entities: FixedStepSimulationSystemGroup даёт фикс-тик «из коробки».

Пример каркаса:
```csharp
public interface IClock { float Delta { get; } bool IsPaused { get; } }
public sealed class SimulationLoop : MonoBehaviour, IClock {
  [SerializeField] float tick = 0.1f; // 10 tps
  float acc; public bool IsPaused { get; private set; }
  public float Delta => IsPaused ? 0f : tick;

  void Update() {
    if (IsPaused) return;
    acc += Time.unscaledDeltaTime;
    while (acc >= tick) {
      acc -= tick;
      // вызываем системные тики по порядку:
      InfectionSystem.Tick(tick);
      EconomySystem.Tick(tick);
      StrategicAI.Tick(tick);
      TacticalAI.Tick(tick);
      EventBus.Flush();
    }
  }
  public void SetPaused(bool v) => IsPaused = v;
}
```

3) Данные и конфиги
-------------------
ScriptableObject → Runtime-модель: SO хранит конфиг, при старте превращаем в чистые объекты.

```csharp
// Data/Techs/TechDef.asset (SO)
[CreateAssetMenu(menuName="Defs/Tech")]
public class TechDef : ScriptableObject {
  public string Id;
  public string Title;
  public float ResearchCost;
  public TechDef[] Prereq;
}

// Domain/Tech/Tech.cs (runtime)
public record Tech(string Id, float Cost, IReadOnlyList<string> Prereq);
```

Почему так: SO — удобен для редактора и баланса; рантайм-копии — безопасны и сериализуемы.

4) Экономика (двойное ВВП)
--------------------------
Идея: два бюджета и две модели принятия решений.

- StateEconomy (ИИ государства): собирает налоги/ресурсы, тратит по стратегическим приоритетам (оборона, санитарные кордоны, базовые исследования).
- PlayerEconomy (личный ВВП): игрок вкладывает в прорывные исследования, постройку укреплений и «точечную поддержку» ИИ.

Мини-API:
```csharp
public interface IEconomy {
  float Balance { get; }
  bool TryReserve(float amount);
  void Commit(float amount);
  void Tick(float dt);
}

public interface IBuildService {
  bool CanPlace(BuildingType type, GridPos at, IEconomy whoPays);
  BuildOrder Place(BuildingType type, GridPos at, IEconomy whoPays);
}
```

5) Инфекция (модель заражения)
------------------------------
Отправная точка: клеточный автомат на сетке (состояния S/I/R или континуум заражения 0..1; вероятности заражения зависят от соседей, плотности, укреплений). Это можно сочетать с вейтом клеток (доля населения/мобильность).

```csharp
// Domain/Infection
public struct CellInfection { public float level; public InfectionState state; }

// Application/InfectionSystem
public sealed class InfectionSystem {
  public void Tick(float dt) {
    // 1) диффузия/заражение по соседям (Von Neumann / Moore)
    // 2) воздействие укреплений/санитарных зон (модификаторы)
    // 3) выздоровление/снижение при ресурсо-вложениях
  }
}
```

Расширения: стохастика, зоны карантина, «очаги», транспортные коридоры и т.п.

6) ИИ: стратегический + тактический (гибрид)
--------------------------------------------
Рекомендован гибрид трёх подходов:

1) Utility AI — быстро оценивает «что важно сейчас» (сдерживать очаг, построить кордон, финансировать исследование) через скоринг 0..1. Лёгкий и объяснимый слой приоритизации.
2) GOAP — строит план для выбранной цели: какие шаги выполнить (собрать ресурсы → исследовать «Щит-I» → построить 3 турели на дуге X). Подходит для «гос-ИИ», который мыслит «целями».
3) Behavior Trees — тактическое «микро»: патрули, наведение, ремонт, локальная оборона, реакции на события. Деревья хорошо отлаживаются и читабельны.

Слой восприятия:
```csharp
public interface IWorldState {
  float InfectionAt(GridPos p);
  float FriendlyStrengthIn(Region r);
  bool IsChokePoint(GridPos p);
  bool HasFortification(GridPos p);
}
```

Utility-оценки (пример):
```csharp
public interface IConsideration { float Score(IWorldState w); } // 0..1
public sealed class HoldLineConsideration : IConsideration {
  public float Score(IWorldState w) {
    // оценка важности удержания линии по метрикам мира
    return 0.5f;
  }
}
```

GOAP (эскиз):
```csharp
public interface IAction {
  bool Preconditions(IWorldState w);
  IWorldState Apply(IWorldState w);
  float Cost(IWorldState w);
}
public interface IPlanner {
  IReadOnlyList<IAction> Plan(Goal g, IWorldState w);
}
```

BT (псевдокод узлов):
```
Selector(
  If(ThreatNear) -> DeploySquad,
  Else -> Repair,
  Else -> Patrol
)
```

7) События, команды, сохранения
-------------------------------
EventBus (асинхронные события, очереди) — чтобы симуляция и UI не были жёстко связаны.
Commands — удобно логировать/откатывать действия (строительство, назначение бюджета).

Мини-EventBus:
```csharp
public static class EventBus {
  static readonly Queue<object> q = new();
  static readonly List<object> subs = new();
  public static void Publish<T>(T e) => q.Enqueue(e);
  public static void Subscribe(object handler) => subs.Add(handler);
  public static void Flush() {
    while (q.Count > 0) {
      var e = q.Dequeue();
      foreach (var h in subs) (h as dynamic).On((dynamic)e);
    }
  }
}
```

Команда строительства:
```csharp
public record PlaceBuildingCmd(BuildingType Type, GridPos At, Guid Who);
public sealed class BuildService : IBuildService {
  public BuildOrder Place(BuildingType type, GridPos at, IEconomy money) {
    if (!CanPlace(type, at, money)) return BuildOrder.Failed;
    money.Commit(type.Cost);
    var order = new BuildOrder(type, at);
    EventBus.Publish(order);
    return order;
  }
}
```

8) Производительность и масштабирование
--------------------------------------
- Начинай с обычных компонентов. Когда агентов станет много — выноси тяжёлые циклы в Jobs + Burst (сканы по сетке, диффузия инфекции, массовая тактика).
- Если карта и ИИ разрастаются — переносить горячие системы (инфекция-диффузия, массовое сканирование карты) в DOTS/Entities c фикс-шагом (FixedStepSimulationSystemGroup).
- Стратегия обновления: стратегический слой реже (раз в N тиков), тактика чаще; тяжёлые оценки разбивай по кадрам.

9) Каркас: план первых 2 недель
-------------------------------
День 1–2
- Создать проекты/папки, пустую сцену.
- Tilemap + конвертер координат (мир⇄сетка).
- SimulationLoop с фикс-тиком и паузой.

День 3–5
- SO-конфиги: BuildingDef, TechDef, InfectionParams.
- Runtime-модели и EconomySystem, InfectionSystem (простейшая CA-логика S/I/R).

День 6–10
- EventBus + базовые команды (PlaceBuilding, StartResearch).
- Utility-скоры для 3 задач гос-ИИ: «сдержать очаг», «застроить горлышко», «вложиться в исследование X».
- Простой GOAP-планер (2–3 действия), BT для патруля/ремонта.

День 11–14
- Сохранение/загрузка GameState в JSON.
- UI: пауза, скорости ×0.5/×1/×2, оверлеи инфекции/контроля.

10) Примеры кодовых скелетов (супер-кратко)
-------------------------------------------
EventBus — см. выше.

Команда строительства — см. выше.

Utility Consideration (ещё один пример):
```csharp
public sealed class OutbreakPressure : IConsideration {
  readonly Region region; readonly float threshold;
  public float Score(IWorldState w) {
    var p = w.InfectionAt(region.Center);
    return Mathf.Clamp01((p - threshold) / (1f - threshold)); // 0..1
  }
}
```

Что дальше?
-----------
- Сгенерировать минимальный Unity-каркас (папки, пустые классы/интерфейсы, шаблонные SO и MonoBehaviour) и чек-лист — чтобы можно было «просто открыть проект и начать писать».
- При желании — адаптация под Godot C# (TileMap, Autoload-синглтоны, Signals вместо EventBus).
