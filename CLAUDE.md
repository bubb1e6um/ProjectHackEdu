# CLAUDE.md — HackEdu Project Context

## Обзор проекта

**HackEdu** — обучающая интерактивная среда с элементами stealth-головоломки на Unity 3D (C#).
Визуализирует работу структур данных (графы, деревья) и алгоритмов в реальном времени.
Игрок управляет объектом, преодолевает препятствия, избегает патрулей и взаимодействует с окружением через внутриигровой терминал, эмулирующий UNIX-подобную ОС.

**Целевая аудитория:** студенты младших курсов технических специальностей.
**Цель:** геймификация изучения системного администрирования, консольных команд и структур данных.

---

## Архитектура и паттерны

Проект построен на принципах SOLID и паттернах GoF:

- **Composite** — Виртуальная Файловая Система (VFS). Файлы и директории трактуются единообразно через базовый класс `VFSNode`. Доступ к дочерним узлам за O(1) через `Dictionary<string, VFSNode>`.
- **Command** — обработка команд терминала. Каждая команда — отдельный класс, реализующий `ICommand`. Новые команды добавляются без модификации парсера (Open/Closed Principle).
- **State (FSM)** — ИИ противников. Поведение дрона определяется текущим `IEnemyState`. Логика патрулирования изолирована от логики тревоги.
- **Singleton** — `GameManager` с `DontDestroyOnLoad` для сохранения состояния между сценами.
- **Неориентированный граф** — сеть устройств уровня. Узлы `NetworkNode` содержат ссылки на `Transform` объектов Unity, связывая математику с 3D-миром.

---

## Структура модулей и скриптов

### Модуль 1: Ядро (Core System)

| Скрипт | Назначение |
|---|---|
| `GameManager.cs` | Singleton. Маршрутизация сцен, управление `Time.timeScale`. Методы: `LoadNextLevel()`, `RestartLevel()`, `LoadMainMenu()`. Использует `DontDestroyOnLoad`. |
| `MainMenuController.cs` | UI главного меню. `StartGame()` загружает сцену с индексом 1. `QuitGame()` завершает приложение. |

### Модуль 2: Виртуальная Файловая Система (VFS)

| Скрипт | Назначение |
|---|---|
| `VFSNode.cs` | **abstract.** Базовый класс Composite. Свойства: `Name`, `Parent`, `Permissions` (UNIX-маска), `Owner`, `Date`. Абстрактный метод `GetSize()`. |
| `DirectoryNode.cs` | Контейнер (наследует `VFSNode`). Хранит `Dictionary<string, VFSNode> _children`. Методы: `AddChild()`, `GetChild()`, `GetChildren()` (возвращает `IEnumerable`). |
| `FileNode.cs` | Leaf-узел (наследует `VFSNode`). Свойство `Content` (текст). `GetSize()` = длина `Content`. |
| `VirtualFileSystem.cs` | Фасад VFS. Хранит `Root` и `CurrentDirectory`. `InitializeMockData()` создаёт начальные папки (`bin`, `logs`) и файлы (`mission_brief.txt`). |

### Модуль 3: Терминал и команды (Terminal Input)

| Скрипт | Назначение |
|---|---|
| `TerminalController.cs` | Контроллер ввода-вывода (View/Controller MVC). Свойства: `isTerminalOpen`, `ActiveConnection` (удалённый узел), `LocalNode` (127.0.0.1). `ToggleTerminal()` переключает видимость, замораживает физику, очищает лог и сбрасывает подключения. История команд через `List<string>`. |
| `ICommand.cs` | Интерфейс. Контракт: `string Execute(string[] args)`. |
| `CommandParser.cs` | Парсер ввода. Использует `Dictionary<string, ICommand>`. Метод `ParseAndExecute(string input)` — сплит по пробелу, поиск команды за O(1), вызов `Execute`. |
| `TerminalCommands.cs` | Реализации команд: `HelpCommand`, `LsCommand`, `CatCommand`, `SshCommand`, `UnlockCommand`, `ScanCommand`, `TreeCommand`, `MkdirCommand` и др. |

**Ключевые алгоритмы в командах:**
- `ScanCommand` — пространственный поиск: итерация по графу устройств, проекция координат на 2D (Y=0), евклидово расстояние, порог ≤ 2f.
- `TreeCommand` — обход дерева в глубину (DFS) через рекурсивную `TraverseTree()` с параметром глубины для отступов.
- `MkdirCommand` — проверка дубликатов ключей в словаре перед созданием `DirectoryNode`.

### Модуль 4: Сетевая топология (Network Graph)

| Скрипт | Назначение |
|---|---|
| `NetworkNode.cs` | Устройство в сети. Свойства: `IP`, `DeviceName`, `IsLocked`, `PhysicalTransform`. Делегат `Action OnUnlock` — физические объекты подписываются для реакции на взлом. |
| `NetworkGraph.cs` | Менеджер графа. `Dictionary<string, NetworkNode>` для доступа по IP за O(1). Методы: `AddNode()`, `GetNode()`, `GetAllNodes()`. |

### Модуль 5: Игровые сущности (Game Entities)

| Скрипт | Назначение |
|---|---|
| `GridMovement.cs` | Перемещение игрока по дискретной сетке. Проверяет `Time.timeScale == 0f` для предотвращения Input Bleeding. `Physics.Raycast` с `LayerMask` перед шагом. Корутина плавной интерполяции. |
| `DoorController.cs` | Физическая преграда. При старте создаёт `NetworkNode`, передаёт свой `Transform`, регистрируется в `NetworkGraph`. Подписка на `OnUnlock` → отключение `Collider` + скрытие `Mesh`. |
| `EnemyController.cs` | ИИ дронов. Хранит `IEnemyState currentState`. В `Update()` делегирует `currentState.UpdateState()`. Переключает режимы: патрулирование ↔ тревога (Game Over). Регистрируется в сети для взлома через терминал. |
| `IEnemyState` | Интерфейс состояний дрона. |

---

## Реализованный функционал (v1.0)

- Grid-based перемещение с Raycast-коллизиями
- Изоляция ввода: блокировка управления + `Time.timeScale = 0` при открытом терминале
- FSM-патрулирование дронов + FOV-обнаружение → Game Over
- Терминал: всплывающий UI, история команд (↑/↓), автоочистка сессии
- VFS: иерархическое дерево в памяти, чтение файлов, навигация, создание папок
- Сканирование сети (евклидово расстояние на 2D-плоскости)
- SSH-соединение с устройствами, удалённые команды (unlock), автоматический разрыв при выходе
- Главное меню, Singleton GameManager, бесшовные переходы между уровнями

---

## Порядок сцен в Build Settings

```
Index 0: MainMenu
Index 1: Level_1  (обучающий)
Index 2: Level_2  (стелс с дроном)
Index N: Level_N  (последующие)
```

`GameManager` должен присутствовать на каждой сцене уровня (индексы 1–N). Singleton предотвращает дублирование при переходах.

---

## Правила для Claude Code

- **Язык:** C#, Unity 3D.
- **Не ломай паттерны:** Composite (VFS), Command (терминал), State (FSM дронов), Singleton (GameManager). Все новые фичи должны следовать этим паттернам.
- **Новые команды терминала:** создавай класс, реализующий `ICommand`, и регистрируй в `CommandParser`. Не модифицируй парсер switch-case'ами.
- **Новые устройства (камеры, турели и т.д.):** создавай контроллер, который регистрирует свой `NetworkNode` в `NetworkGraph` и подписывается на `OnUnlock`.
- **Новые состояния дронов:** создавай класс, реализующий `IEnemyState`.
- **VFS:** используй `DirectoryNode` и `FileNode`. Доступ к детям через `Dictionary`, не через списки.
- **Input Bleeding:** всегда проверяй `Time.timeScale` перед обработкой ввода игрока.
- **Сетевой граф:** доступ к узлам по IP через `Dictionary<string, NetworkNode>`. Расстояние считать по 2D (обнулять Y).

---

## Аудиосистема — инструкция по добавлению звуков

### Архитектура

Настройки громкости хранятся в `MainMenuController` (поля `_masterVolume`, `_sfxVolume`, `_musicVolume`) и применяются через:
- `AudioListener.volume` — мастер-громкость (глобально)
- `MusicSource.volume` — громкость музыки (публичное поле `MainMenuController.MusicSource`)
- SFX-громкость пока хранится как значение; при добавлении AudioManager подключается вручную (см. ниже)

### Как добавить фоновую музыку

1. Создай GameObject `[MusicPlayer]` на сцене `MainMenu`.
2. Добавь компонент `AudioSource`. Параметры: `Loop = true`, `Play On Awake = true`, назначь AudioClip.
3. В Inspector выбери объект `[MainMenu]` → компонент `MainMenuController` → поле **Music Source** → перетащи `[MusicPlayer]`.
4. Готово. Слайдер **Music Volume** в настройках будет писать в `MusicSource.volume` автоматически.

### Как добавить SFX (звуки клавиш, тревога дрона и т.д.)

1. Создай `AudioManager.cs` — Singleton (`DontDestroyOnLoad`), аналогично `GameManager`.
2. Добавь публичное поле `public float SfxVolume { get; private set; } = 1f;` и метод `SetSfxVolume(float v)`.
3. В `MainMenuController.cs` найди колбэк SFX Volume (строка `_sfxVolume = v`) и замени комментарий на:
   ```csharp
   v => { _sfxVolume = v; AudioManager.Instance?.SetSfxVolume(v); }
   ```
4. В каждом скрипте, воспроизводящем SFX (`AudioSource.PlayOneShot`), умножай громкость на `AudioManager.Instance.SfxVolume`.
5. **Пример для тревоги дрона** (`EnemyController.cs`):
   ```csharp
   _audioSource.PlayOneShot(alarmClip, AudioManager.Instance?.SfxVolume ?? 1f);
   ```

### Правила для Claude Code

- Не хардкодь громкость в `AudioSource.PlayOneShot`. Всегда передавай `AudioManager.Instance?.SfxVolume ?? 1f`.
- `MusicSource` назначается только через Inspector; не ищи его через `FindObjectOfType` в рантайме.
- При добавлении AudioManager используй паттерн Singleton с `DontDestroyOnLoad` (см. `GameManager.cs`).

---

## Перспективы развития

- Поддержка флагов в парсере (например `ls -l`, `scan -v`) — расширение `CommandParser` + логика ветвления внутри `ICommand`.
- Новые сущности (камеры, турели) — новый контроллер + регистрация IP в `NetworkGraph`.
- Механика шифрования — связь текстовых файлов VFS (пароли) с `NetworkNode.Unlock()`, требование аргумента-пароля (`unlock 1234`).
