# MyShell

Un shell de Windows propio, construido para poder seguir creciendo sin
reescribirlo. Este documento explica el porqué de la arquitectura, no solo
el qué.

## Idea central: un solo flag, dos modos de ejecución

`ShellMode` (`DevOverlay` / `FullShell`) es la única bifurcación entre
"correr como app normal encima de explorer.exe, para iterar rápido y sin
riesgo" y "ser literalmente el shell que Winlogon lanza". Todo el código de
negocio (módulos, widgets) es agnóstico a esto. Solo `ShellModeManager` sabe
la diferencia:

- **DevOverlay** (por defecto, `dotnet run` o F5): oculta la taskbar real al
  arrancar, la restaura al salir o si algo revienta (`UnhandledException`,
  `DispatcherUnhandledException`). Así puedes romper cosas sin perder el
  escritorio real.
- **FullShell** (`--full-shell`, solo cuando Winlogon te lanza así): no hay
  taskbar real que ocultar/restaurar; salir de este proceso cierra la
  sesión, así que el manejo de errores aquí no es cosmético.

**No pruebes FullShell en tu máquina principal.** Usa una VM con snapshot.
`tools/register-shell.ps1` hace backup del valor de registro antes de
tocarlo y tiene un `-Revert`, pero un shell que crashea en el logon te puede
dejar sin escritorio antes de llegar a ejecutar ese revert — lee las notas
de recuperación en la cabecera del script.

## Capas

```
MyShell.Core            <- Win32/interop puro + contratos + event bus.
                            No sabe qué es un "widget de volumen".
MyShell.Modules.*        <- Un módulo = una feature (taskbar, tray,
                            controles del sistema, start menu, clock).
MyShell.Host             <- Composition root: decide el modo, registra
                            módulos, dibuja la ventana de la barra.
```

**Regla práctica para cuando sigamos trabajando en esto:** una feature
nueva casi siempre es un módulo nuevo (`IShellModule`), no un parche dentro
de `Host`. Si te encuentras editando `App.xaml.cs` o `ShellWindow.xaml.cs`
para algo que no sea "registrar un módulo" o "layout genérico", es señal de
que esa lógica se está colando donde no debería.

## El event bus, no referencias directas

Los módulos no se llaman entre sí ni tocan el Host directamente — publican y
escuchan eventos (`WindowOpenedEvent`, `TrayIconAddedEvent`, etc.) via
`IEventBus`. Esto es lo que permite añadir/quitar módulos sin que el resto
se entere, y probar un módulo aislado sin levantar toda la shell.

## Qué está completo vs. qué es un esqueleto a propósito

| Pieza | Estado |
|---|---|
| AppBar (dock de la barra) | Funcional — es lo que ya tenías funcionando |
| WindowWatcher (apps abiertas sin depender de explorer) | Funcional |
| ModuleRegistry / EventBus | Funcional |
| **App.xaml.cs / ShellWindow.xaml.cs (composition root)** | **Funcional** — no existían; ver "Composition root" más abajo |
| **Barra visual (colores, tipografía, layout)** | **Funcional** — restilizada para calzar con el diseño de referencia; ver "Estilo de la barra" |
| TrayHost (bandeja del sistema) | Registra la ventana `Shell_TrayWnd` correctamente, pero el parseo de `Shell_NotifyIcon` está marcado como TODO — es protocolo no documentado por Microsoft. Ver comentarios en `TrayHost.cs` para qué implementación de referencia portar (RetroBar o Cairo Shell) |
| SystemControls: batería | **Funcional** — `GetSystemPowerStatus`, se autooculta en desktops sin batería |
| SystemControls: Wifi/Bluetooth | Ícono estático, sin estado real — ver TODO en `SystemControlsModule.cs` |
| SystemControls: volumen/brillo | Placeholder — mueve aquí tu código existente de `QuickSettingsTray` |
| StartMenu | Enumeración básica de `.lnk`; falta portar el filtro de apps ocultas que ya tenías. El widget de búsqueda es solo chrome visual, no abre nada aún |
| Clock | **Funcional** — fecha centrada, en español fijo, no sigue el idioma de Windows |
| Desktop icons, Alt+Tab propio, escritorios virtuales | No empezado — necesarios solo si vas a `FullShell` en serio |
| DI container | Deliberadamente no añadido aún — con 5 módulos, wiring manual es más simple que añadir un contenedor. Revisar si esto cambia. |

## Próximos pasos sugeridos, en orden

1. Portar volumen/brillo del proyecto viejo a `SystemControlsModule`
   (un widget a la vez).
2. Portar el estado real de Wifi/Bluetooth (`Windows.Devices.WiFi` /
   `Windows.Devices.Bluetooth`) — hoy son solo íconos estáticos.
3. Rellenar el parseo de `TrayHost` usando RetroBar como referencia.
4. Probar `DevOverlay` a diario una temporada — es donde vive el 90% del
   valor con el 10% del riesgo.
5. Solo cuando lo anterior sea sólido, montar una VM y probar `FullShell`.

---

## Composition root (antes no existía)

Hasta esta pasada, el proyecto no tenía `App.xaml.cs` ni
`ShellWindow.xaml.cs`: nada tomaba los módulos registrados y los pintaba
dentro de los `StackPanel` de la ventana. Ahora sí:

- **`App.xaml.cs`** — punto de entrada. Lee `--full-shell`, arma el
  `EventBus`, registra los módulos en `ModuleRegistry`, arranca
  `ShellModeManager` y crea la ventana.

  `TaskbarModule` (la lista de apps abiertas) se deja **fuera** del
  registro a propósito: el diseño de referencia solo muestra la fecha en
  el centro de la barra, sin lista de apps. Está comentado ahí mismo cómo
  agregarlo de vuelta — solo hay que darle un `Order` distinto al de
  `ClockModule` para que no compitan por el mismo dock central.

- **`ShellWindow.xaml.cs`** — toma `ModuleRegistry.CollectWidgets()`, los
  reparte en Left/Center/Right/Tray según `PreferredDock` y `Order`, y
  engancha el `AppBar` ya existente para reservar el espacio en pantalla.

## Estilo de la barra

La barra se restilizó para verse: fondo oscuro (`#12131A`), 32px de alto
(antes 40, ver `appsettings.json`), tipografía `Segoe UI Variable Text`, e
íconos `Segoe Fluent Icons`. Los recursos compartidos
(`BarForegroundBrush`, `BarIconFontFamily`, `BarTextButtonStyle`, etc.)
viven en `ShellWindow.xaml` y todos los widgets los referencian vía
`SetResourceReference` en lugar de hardcodear colores — cambiar el look de
toda la barra es editar ese único bloque de recursos.

De izquierda a derecha:

- **Izquierda**: lupa + "Search" (`StartMenuModule` → `SearchButtonWidget`),
  sin fondo, solo hover sutil.
- **Centro**: fecha en español fijo, tipo "Sábado, 29 Agosto"
  (`ClockModule`, módulo nuevo — ver por qué es módulo propio en "Capas"
  más arriba).
- **Derecha**: Wifi, Bluetooth, batería (funcional, con `%` real y se
  esconde sola si no hay batería), un separador vertical, y el botón de
  power (`SystemControlsModule`).

Ni el botón de búsqueda ni el de power abren nada todavía — son chrome
visual con un `TODO` marcado en el código para cuando construyas esas
superficies (launcher, flyout de apagado).

## Cómo aplicar/probar

```
dotnet build
dotnet run --project src/MyShell.Host
```

Eso corre en `DevOverlay` (seguro para tu máquina principal). No se pudo
compilar esto en un entorno Linux al generarlo — WPF es Windows-only —, así
que vale la pena correr `dotnet build` apenas lo pegues por si aparece algún
detalle de compilación.
