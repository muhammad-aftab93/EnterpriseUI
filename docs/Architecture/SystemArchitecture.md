# EnterpriseUI Architecture & Systems Design Guide

## 1. Architectural Philosophy

EnterpriseUI is built from the ground up on modern .NET software engineering principles:

- **Clean Architecture & Domain Segregation**: UI rendering is strictly an infrastructure concern. Component state, query compilation, virtualization math, theme tokens, and accessibility are pure domain models.
- **Dependency Inversion**: Core components depend solely on abstractions (`IUIComponent`, `IInteractiveControl`, `IDataSource<T>`, `ITheme`, `IAccessibleControl`), never on platform-specific UI controls (like WinForms `Control`, WPF `DependencyObject`, or Blazor `ComponentBase`).
- **AOT & Trimming-Friendly**: Designed for Native AOT and trimming optimization with suppression attributes and statically analyzable property graphs.
- **Zero-Allocation Hot Paths**: The query engine and virtualization algorithms avoid per-frame allocations during scrolling and viewport updates.

---

## 2. C4 Architecture Models

### 2.1 Level 1: System Context EnterpriseDiagram

```mermaid
flowchart TD
    User["Enterprise End-User<br/>(Desktop, Web, Mobile)"]
    Dev["Developer / Solution Architect"]
    
    subgraph EnterpriseUI["EnterpriseUI Platform Ecosystem"]
        App["Host Application<br/>(WinForms, WPF, Blazor, MAUI)"]
        Components["EnterpriseUI Component Suite"]
        Tooling["EnterpriseUI Designer & Scaffolding Tools"]
    end
    
    Backend["Enterprise Backend Services<br/>(REST API, OData v4, GraphQL, EF Core)"]
    AIProvider["AI Service Provider<br/>(Azure OpenAI, OpenAI, Local LLM)"]

    User -->|Interacts with UI| App
    Dev -->|Configures & Builds with| Components
    Dev -->|Designs Themes with| Tooling
    App -->|Binds to| Components
    Components -->|Executes Queries against| Backend
    Components -->|Streams Prompts to| AIProvider
```

### 2.2 Level 2: Container EnterpriseDiagram (Layered Architecture)

```mermaid
flowchart TB
    subgraph PlatformLayer["Platform Rendering Adapters"]
        Blazor["EnterpriseUI.Blazor"]
        WinForms["EnterpriseUI.WinForms"]
        WPF["EnterpriseUI.Wpf"]
        Maui["EnterpriseUI.Maui"]
        AspNetCore["EnterpriseUI.AspNetCore"]
    end

    subgraph ComponentLayer["Enterprise UI Components"]
        Grids["EnterpriseUI.Grids"]
        Charts["EnterpriseUI.Charts"]
        Editors["EnterpriseUI.Editors"]
        Nav["EnterpriseUI.Navigation"]
        Layout["EnterpriseUI.Layout"]
        Sched["EnterpriseUI.Scheduling"]
        Office["EnterpriseUI.Office"]
        AI["EnterpriseUI.AI"]
    end

    subgraph ServiceLayer["Cross-Cutting Enterprise Services"]
        Theming["EnterpriseUI.Theming"]
        Icons["EnterpriseUI.Icons"]
        DataEngine["EnterpriseUI.Data (Query & Virtualization)"]
        A11y["EnterpriseUI.Accessibility"]
        Validation["EnterpriseUI.Validation"]
        Export["EnterpriseUI.Exporting"]
        Security["EnterpriseUI.Security"]
    end

    subgraph CoreLayer["Foundational Contracts"]
        Abstractions["EnterpriseUI.Abstractions"]
        Core["EnterpriseUI.Core (MVVM, Events, Licensing)"]
    end

    PlatformLayer --> ComponentLayer
    ComponentLayer --> ServiceLayer
    ServiceLayer --> CoreLayer
```

---

## 3. Unified Rendering Pipeline

The rendering pipeline decouples component state and layout calculations from visual drawing:

```mermaid
sequenceDiagram
    autonumber
    actor User as User / Host App
    participant Control as Enterprise Control (e.g., DataGrid)
    participant Data as QueryEngine / DataSource
    participant Virt as VirtualizationManager
    participant Theme as ThemeManager
    participant Renderer as Platform Renderer (WinForms / Blazor / WPF)

    User->>Control: Set DataSource / Apply Filters
    Control->>Data: Execute(DataSourceRequest)
    Data-->>Control: Return DataSourceResult (paged/sorted)
    Control->>Virt: CalculateVisibleRows(scrollTop, viewportH, totalRows)
    Virt-->>Control: Return IndexRange(Start, End)
    Control->>Theme: ResolveToken(BorderColor, PrimaryColor)
    Theme-->>Control: Return Design Tokens
    Control->>Renderer: Invalidate / Trigger Platform Draw
    Renderer->>Renderer: Paint Double-Buffered Virtual Elements
```

---

## 4. Component Lifecycle Architecture

Every component in EnterpriseUI follows a deterministic lifecycle:

```mermaid
stateDiagram-v2
    [*] --> Instantiated: Constructor()
    Instantiated --> Initializing: InitializeAsync()
    Initializing --> DataBinding: Bind DataSource
    DataBinding --> LayoutMeasuring: UpdateViewport() / Measure()
    LayoutMeasuring --> Active: Ready for Interaction
    
    Active --> FilteringOrSorting: Query Event Triggered
    FilteringOrSorting --> DataBinding: Execute Query
    
    Active --> ThemeChanged: ThemeManager.ApplyTheme()
    ThemeChanged --> LayoutMeasuring: Recalculate Styles
    
    Active --> Disposing: Dispose() / DisposeAsync()
    Disposing --> [*]: Unsubscribe Events & Free Buffers
```

---

## 5. Extensibility & Inversion of Control

All components implement extensible interfaces allowing developers to swap any layer:

1. **Custom Data Providers**: Implement `IDataSource<T>` to hook into custom gRPC streams, Couchbase, or Elasticsearch.
2. **Custom Renderers**: Implement platform-specific renderers by consuming `RenderNode` or native paint events.
3. **Custom Theme Tokens**: Inherit existing tokens or register new design tokens with `ThemeBuilder`.
4. **Custom Validators**: Leverage fluent async rules with `Validator<T>.RuleForAsync(...)`.
5. **Security Gating**: Controls implement `IAuthorizedControl` to automatically hide or disable themselves based on current `ClaimsPrincipal` roles or permission sets.
