# DMC KBM Mod (DMC4 / DMC4SE, DMC HD Collection y DmC)

> 🌐 **Idioma:** [English](README.md) | **Español**

Mod ligero y portable de **Teclado y Ratón (KBM)** para **Devil May Cry 4**, **Devil May Cry 4 Special Edition**, **Devil May Cry HD Collection (DMC1, DMC2 y DMC3)** y **DmC: Devil May Cry (2013)** en Windows.

Funciona en segundo plano sin modificar ni sobrescribir ningún archivo de los juegos, por lo que es **100% compatible** con mods basados en `dinput8.dll` como **DDMK**, **DMC3 Crimson**, **dmc4_hook** y **LauncherFix**.

---

## Características principales

- **Detección automática del juego activo** (solo intercepta teclado/ratón cuando la ventana del juego está en primer plano):
  - `DevilMayCry4SpecialEdition.exe` (DMC4 SE)
  - `DevilMayCry4_DX9.exe` / `DevilMayCry4_DX10.exe` (DMC4 2008)
  - `dmc1.exe`, `dmc2.exe`, `dmc3.exe` (DMC HD Collection — deja libre `dmcLauncher.exe` para usar el ratón normal en el selector)
  - `DMC-DevilMayCry.exe` (DmC: Devil May Cry 2013 — mantiene su cámara nativa por ratón mientras unifica el switch Estilo DMC5 / Clásico)
- **Switch instantáneo entre 2 esquemas de controles** (desde la ventana, desde la bandeja del sistema o en plena partida con **`F2`**):
  - **Estilo DMC5**: `Clic Izq` Espada · `Clic Der` Estilo/Buster · `Botón Lateral 4 / Clic Medio` Pistola
  - **Estilo Clásico**: `Clic Izq` Espada · `Clic Der` Pistola · `Botón Lateral 4 / Clic Medio` Estilo/Buster
- **Compatible con DirectInput**: envía códigos de escaneo de hardware (`KEYEVENTF_SCANCODE` + `EXTENDEDKEY`) para que MT Framework (DMC4/SE) y DMC HD Collection respondan siempre, incluyendo mantener pulsado (Charge Shot, Exceed, Lock-On, Royal Guard).
- **Cámara con ratón por Raw Input (`WM_INPUT`)**: sensibilidad X/Y independiente, zona muerta, suavizado de retención, inversión de ejes, modo solo horizontal para DMC1/2/3 y confinamiento opcional del cursor a la ventana del juego (`ClipCursor`).
- **Interfaz bilingüe (`EN ⇄ ES`)**: detecta el idioma de Windows automáticamente y permite cambiar entre inglés y español en un clic.
- **Probador en Vivo integrado**: te permite probar los controles, el switch DMC5 ⇄ Clásico y la cámara por ratón **hoy mismo sin tener los juegos instalados**.
- **Sin dependencias externas**: `DMC_KBM_Mod.exe` es portable (~80 KB) y guarda toda la configuración en `dmc_kbm_config.ini`.

---

## Controles por defecto

Movimiento (`W, A, S, D`) y Estilos de Dante (`1, 2, 3, 4` en DMC4 / DDMK) funcionan de forma nativa.

| Acción | Estilo DMC5 | Estilo Clásico | DMC4 / SE | DMC1/2/3 HDC | DmC (2013) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Espada / Melee** | `Clic Izq` | `Clic Izq` | `I` | `I` | `LMB` (nativo) |
| **Estilo / Buster / Especial** | `Clic Der` | `Ratón Lat. 4` / `Clic Medio` | `L` | `L` | `F` (Especial/Lanzar) |
| **Pistola / Disparo** | `Ratón Lat. 4` / `Clic Medio` | `Clic Der` | `J` | `J` | `RMB` |
| **Saltar** | `Espacio` | `Espacio` | `K` | `K` | `Espacio` (nativo) |
| **Lock-On / Esquivar (DmC)** | `Shift Izq` | `Shift Izq` | `Espacio` | `Espacio` | `Shift Izq` (nativo) |
| **Devil Trigger** | `F` | `F` | `N` | `F` | `V` |
| **Pistola / Exceed / Ángel** | `Q` / `Rueda Arriba` | `Q` / `Rueda Arriba` | `Q` | `Q` | `Q` (Modo Ángel) |
| **Arma Melee / Demonio** | `E` / `Rueda Abajo` | `E` / `Rueda Abajo` | `E` | `E` | `E` (Modo Demonio) |
| **Centrar Cámara** | `Ratón Lat. 5` / `R` | `Ratón Lat. 5` / `R` | `P` | `C` | `C` |
| **Cambiar Objetivo** | `Tab` / `C` | `Tab` / `C` | `O` | `O` | `Tab` |
| **Burla (Taunt)** | `V` | `V` | `M` | `M` | `T` |
| **Mover Cámara** | `Ratón` | `Ratón` | `Flechas` | `Flechas` (H en DMC3) | Ratón nativo |

> Todos los controles de ambos esquemas y las teclas internas de cada juego se pueden reasignar desde la interfaz o editando `dmc_kbm_config.ini`.

---

## Atajos en juego

- **`F1`**: Activar / Pausar el mod (útil en menús o si quieres usar el ratón libremente).
- **`F2`**: Cambiar al vuelo entre **Estilo DMC5** y **Estilo Clásico** (muestra un aviso discreto en la esquina sin sacar el foco del juego).
- **`F3`**: Activar / Desactivar la cámara con ratón (mantiene activos los botones del ratón).

---

## Cómo usarlo

1. Descarga **`DMC_KBM_Mod.exe`** desde [Releases](https://github.com/aanderdc/dmc-kbm-mod/releases) y ejecútalo (no requiere instalación).
2. Elige **Estilo DMC5** o **Estilo Clásico** en la parte superior (o cámbialo cuando quieras con `F2`).
3. *(Opcional)* Abre la pestaña **Probador en Vivo** y pulsa **Activar Modo Prueba Local** para probar clics, rueda, teclado y movimiento de cámara ahora mismo sin tener el juego instalado.
4. Cuando instales DMC4 / DMC4SE o DMC HD Collection, deja `DMC_KBM_Mod.exe` abierto (puedes minimizarlo a la bandeja del sistema) y abre el juego con sus controles de teclado por defecto.

---

## Solución de problemas y compilación

- **El juego no recibe los clics o teclas del mod:** si lanzas DMC4 o Steam como Administrador, Windows bloquea la entrada desde apps sin privilegios (UIPI). Pulsa el botón **"Reiniciar Admin"** en la barra superior de `DMC_KBM_Mod.exe`.
- **Cambiaste las teclas dentro de las opciones del juego:** abre la pestaña **Perfiles por Juego** y ajusta las teclas reales que espera ese juego concreto.
- **Recompilar desde el código fuente:** ejecuta `powershell -ExecutionPolicy Bypass -File build.ps1` (usa el compilador `csc.exe` integrado en Windows, no requiere Visual Studio).
- **Ejecutar pruebas automáticas:** ejecuta `powershell -ExecutionPolicy Bypass -File tests\test_suite.ps1`.
