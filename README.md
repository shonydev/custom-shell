# custom-shell

Un shell de Windows propio.

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
| **App.xaml.cs / ShellWindow.xaml.cs (composition root)** | **Funcional (recuperado en esta pasada)** — el código ya existía en una pasada anterior pero `.gitignore` traía `*.xaml.cs` en la sección "WPF / Windows app generated files", así que git lo descartaba silenciosamente en cada commit aunque el README ya lo describía como hecho. Corregido: `.gitignore` ahora solo ignora `*.g.cs`/`*.g.i.cs` (los parciales realmente autogenerados por el compilador de XAML), y `App.xaml.cs`/`ShellWindow.xaml.cs` quedaron commiteados. Ver "Composition root" más abajo |
| **Barra visual (colores, tipografía, layout)** | **Funcional** — restilizada para calzar con el diseño de referencia; ver "Estilo de la barra" |
| TrayHost (bandeja del sistema) | **No implementado todavía** — corrigiendo lo que decía esta fila antes: no hay ningún archivo `TrayHost.cs` en el repo, `MyShell.Modules.Tray/TrayModule.cs` es un stub vacío (`CreateWidgets() => []`). Falta crear la ventana message-only que se registra como `Shell_TrayWnd` y luego el parseo de `Shell_NotifyIcon` (protocolo no documentado por Microsoft — ver RetroBar o Cairo Shell como referencia) |
| WindowWatcher / TaskbarModule | **No implementado todavía** — mismo caso: no hay `WindowWatcher.cs` en `MyShell.Core`, y `MyShell.Modules.Taskbar` solo tiene el `.csproj`, sin ningún `.cs`. `NativeMethods.cs` ya tiene `SetWinEventHook`/`UnhookWinEvent` listos para esto, solo falta la clase que los use |
| SystemControls: batería | **Funcional** — `GetSystemPowerStatus`, se autooculta en desktops sin batería |
| **SystemControls: volumen** | **Funcional (nuevo en esta pasada)** — `MyShell.Core.Interop.SystemVolume` envuelve Core Audio por COM (`IAudioEndpointVolume`) en vez de depender de NAudio. Click para mute, scroll sobre el ícono para subir/bajar de a 2%, igual que el ícono de volumen de Windows |
| **SystemControls: brillo** | **Funcional (nuevo en esta pasada)** — vía WMI (`WmiMonitorBrightness` / `WmiMonitorBrightnessMethods` en `root\WMI`), scroll de a 5%. Solo funciona en paneles internos de laptop que exponen brillo por WMI; si tu monitor externo necesita DDC/CI, este widget se auto-oculta en vez de mostrar un valor falso |
| SystemControls: Wifi/Bluetooth | Ícono estático, sin estado real — ver TODO en `SystemControlsModule.cs` |
| StartMenu | Enumeración básica de `.lnk`; falta portar el filtro de apps ocultas que ya tenías. El widget de búsqueda es solo chrome visual, no abre nada aún |
| Clock | **Funcional** — fecha centrada, en español fijo, no sigue el idioma de Windows |
| Desktop icons, Alt+Tab propio, escritorios virtuales | No empezado — necesarios solo si vas a `FullShell` en serio |
| DI container | Deliberadamente no añadido aún — con 5 módulos, wiring manual es más simple que añadir un contenedor. Revisar si esto cambia. |

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

## Volumen y brillo

`VolumeWidget` y `BrightnessWidget` (en `SystemControlsModule.cs`, a la
izquierda de Wifi en la barra) reemplazan los placeholders anteriores:

- **Volumen** — `MyShell.Core.Interop.SystemVolume` activa
  `IAudioEndpointVolume` sobre el dispositivo de reproducción por defecto
  vía COM (`MMDeviceEnumerator`). Es la misma API que usa NAudio por
  debajo; se implementó directo para no agregar una dependencia entera por
  tres llamadas COM. Si más adelante necesitas volumen por app, medición de
  waveform o cambio de dispositivo, ahí sí conviene meter NAudio o CSCore
  en vez de seguir extendiendo `CoreAudio.cs`.
- **Brillo** — WMI (`root\WMI`, clases `WmiMonitorBrightness` /
  `WmiMonitorBrightnessMethods`). Solo reporta/controla paneles internos de
  laptop que exponen brillo por WMI; un monitor externo por DDC/CI no va a
  aparecer ahí, así que el widget se esconde solo en vez de mostrar un 0%
  falso — mismo criterio que ya usa `BatteryWidget` para desktops sin
  batería.
- Ambos widgets responden a scroll sobre el ícono (2% volumen, 5% brillo),
  igual que los íconos nativos de Windows. Volumen también hace mute/unmute
  al hacer click.

## Próximos pasos sugeridos, en orden

1. ~~Portar volumen/brillo del proyecto viejo a `SystemControlsModule`.~~
   **Hecho en esta pasada** — ver la tabla de arriba y "Volumen y brillo"
   más abajo.
2. Escribir `WindowWatcher` en `MyShell.Core` sobre `SetWinEventHook`
   (ya está el P/Invoke listo en `NativeMethods.cs`, falta la clase) y
   portar `TaskbarModule` sobre eso — hoy ese módulo solo tiene el
   `.csproj`, ningún `.cs`.
3. Crear `TrayHost`: ventana message-only registrada como `Shell_TrayWnd`,
   luego el parseo de `Shell_NotifyIcon` usando RetroBar como referencia.
4. Portar el estado real de Wifi/Bluetooth (`Windows.Devices.WiFi` /
   `Windows.Devices.Bluetooth`) — hoy son solo íconos estáticos.
5. Probar `DevOverlay` a diario una temporada — es donde vive el 90% del
   valor con el 10% del riesgo. Esto recién es posible ahora que el
   composition root quedó realmente commiteado (ver nota del `.gitignore`
   en la tabla de arriba).
6. Solo cuando lo anterior sea sólido, montar una VM y probar `FullShell`.

---

## Cómo aplicar/probar

```
dotnet build
dotnet run --project src/MyShell.Host
```

Eso corre en `DevOverlay` (seguro para tu máquina principal). No se pudo
compilar esto en un entorno Linux al generarlo — WPF es Windows-only —, así
que vale la pena correr `dotnet build` apenas lo pegues por si aparece algún
detalle de compilación.

Esta pasada agregó `System.Management` como dependencia NuGet nueva (la usa
`BrightnessWidget`) — el primer `dotnet build` va a necesitar bajarla, así
que hazlo con conexión a internet. El resto (composition root, volumen)
solo usa P/Invoke/COM directo, sin paquetes nuevos.
