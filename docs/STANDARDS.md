# Project Standards

- Layers
  - Domain: бизнес-логика, POCO, без Unity API
  - Application: сервисы/UseCases, координация, без UI
  - Infrastructure: сериализация, путь/сеть, адаптеры
  - Presentation: MonoBehaviour, UI, сцены
- C# Naming
  - Types/Methods: PascalCase; fields/properties: camelCase/Prop; const: PascalCase; enums: PascalCase
  - Files = type names; 1 публичный тип на файл
- Folders (Unity)
  - Assets/Scripts/{Domain|Application|Infrastructure|Presentation}
  - Assets/ScriptableObjects/Configs for *Config ScriptableObjects
- ScriptableObjects
  - Имя: *Config; только данные; без логики
- Commits/Branches
  - feat/*, fix/*, chore/*; коммит: imperative (ru/en), кратко + контекст
- Code Style
  - Без статики, где нужен DI; интерфейсы на границах; неизменяемые DTO