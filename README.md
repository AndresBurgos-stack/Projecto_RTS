# ProjectoRTS

Prototipo de RTS 3D low-poly (griegos vs romanos) en Unity 6.

## Estado del proyecto

- Editor: **Unity 6000.0.58f2** (6.0 LTS). Su soporte terminó el 16 de octubre de 2026.
- Versión recomendada: **Unity 6.3 LTS (6000.3.27f1)**, con soporte hasta diciembre de 2027.
- Render: **URP 17.0.4** (paquete integrado del editor).
- UI: **uGUI 2.0.0** (paquete integrado del editor).
- Navegación: **AI Navigation 2.0.0**, embebido en `Packages/com.unity.ai.navigation`.
- Input: **Input System 1.20.1**.
- Cámara: **Cinemachine 3.1.7** (con Splines 2.8.1 como dependencia).
- Timeline: pendiente de añadir si se necesita para cinemáticas.
- Colocación de edificios: **implementada** con preview, raycast, snap al grid, validación de celdas y rotación con `R` (edificios de huella cuadrada).
- Escena de prueba: `Assets/Scenes/PlacementDemo.unity`.
- Paso 1 del roadmap: **completado** (terreno, grid y colocación de muro, puerta, torre, casa, cuartel y almacén griegos).
- Paso 2 del roadmap: **completado** (unidades Hoplita arcadio, Psiloi y Trabajador, cuartel con reclutamiento y panel uGUI).
- Paso 3 del roadmap: **completado** (NavMesh horneado, selección con clic/arrastre y movimiento con clic derecho).
- Paso 4 del roadmap: **completado** (combate automático y por orden, barras de vida, feedback de daño y muerte).
- Paso 5 del roadmap: **completado** (oleadas romanas con spawner en el borde del mapa, avance hacia el Panteón e IA con prioridad soldados → edificios → Panteón).
- Economía: **implementada** (madera y bronce, costes en construcción y reclutamiento, botones deshabilitados sin recursos y HUD de recursos).
- Recursos: **implementados** (nodos de madera y bronce repartidos por el mapa, recolección por trabajadores y depósito en Ayuntamiento, cuartel o almacén).
- Trabajadores: **implementados** (construcción de 1 trabajador por obra, recolección con prioridad Madera/Bronce asignable desde el panel derecho, huida de enemigos).
- Muros y arqueros: **implementados** (toxotes montables sobre muros con clic derecho, traslado entre tramos conectados, torres con ataque a distancia, almenas y rampa con rotación).
- Mapa ampliado a 300x300 con cámara RTS ajustada para recorrerlo.

La primera importación puede tardar varios minutos porque UPM extrae URP, Shader Graph y sus dependencias. Las siguientes aperturas son mucho más rápidas.

Para actualizar el editor: instala `6000.3.27f1` desde Unity Hub y abre el proyecto con esa versión. Al ser un proyecto nuevo, el upgrade debería ser limpio.

## Scripts base

### `Assets/_Project/Scripts/Units/UnitDefinition.cs`

ScriptableObject con los datos de cada tipo de unidad: facción, rol, vida, daño, rango, intervalo de ataque, reducción de daño, movimiento, coste, tiempo de entrenamiento y población.

Crear assets con:

`Assets > Create > ProjectoRTS > Units > Unit Definition`

### `Assets/_Project/Scripts/Units/UnitRegistry.cs`

Registro estático de unidades en runtime. La selección, la búsqueda de objetivos y las barras de vida lo usan en lugar de recorrer la escena con `FindObjectsByType`. Se limpia automáticamente al entrar en Play y elimina las unidades destruidas cuando consulta la lista.

### `Assets/_Project/Scripts/Units/UnitRuntime.cs`

Componente de instancia de una unidad. Guarda la `UnitDefinition`, inicializa la vida actual y expone `MaxHealth`, `CurrentHealth`, `HealthNormalized`, `IsAlive`, `Movement` y `Definition`. Aplica daño con la reducción correspondiente (`ApplyDamage`) y emite eventos `HealthChanged`, `Damaged` y `Died`. Al morir desactiva el agente y los colliders, encoge la unidad y la destruye tras `corpseDuration`.

### `Assets/_Project/Scripts/Units/UnitMovement.cs`

Envuelve el `NavMeshAgent` de la unidad. Aplica velocidad, giro, aceleración y distancia de frenado desde la `UnitDefinition`, mueve la unidad con `SetDestination`, la orienta hacia un objetivo (`FaceTowards`) y muestra u oculta el anillo de selección. `IsMoving` permite que el combate distinga entre una orden de movimiento y una unidad detenida.

### `Assets/_Project/Scripts/Units/UnitSelectionController.cs`

Gestiona la selección y las órdenes:

- Clic izquierdo sobre una unidad griega: selección individual.
- Arrastre con clic izquierdo: selección rectangular de varias unidades.
- Clic derecho sobre el suelo: mueve la selección en formación, con un pequeño espaciado entre unidades.
- Clic derecho sobre una unidad romana: ordena atacar al enemigo; las unidades lo persiguen hasta estar en rango.
- `Escape`: limpia la selección.
- Dibuja un rectángulo translúcido sobre el canvas durante el arrastre.
- Ignora los clics sobre la UI y no actúa mientras se está colocando un edificio.
- Usa `UnitRegistry` y `Physics.RaycastNonAlloc`, sin reservar memoria en cada clic.

### Navegación

La escena incluye un objeto `NavMesh Surface` sobre el terreno. El NavMesh se hornea en el editor y se guarda en `Assets/_Project/Data/NavMesh/PlacementDemoNavMesh.asset`. Los edificios llevan un `NavMeshObstacle` con carving, de modo que al colocarlos bloquean el paso de las unidades.

### `Assets/_Project/Scripts/Combat/UnitCombat.cs`

Combate básico de las unidades. Mientras están quietas adquiere automáticamente al enemigo vivo más cercano dentro de `attackRange + rangePadding`. Si recibe una orden de ataque, persigue al objetivo con `NavMeshAgent` y ataca al llegar. Respeta `attackInterval` y aplica `attackDamage` con la reducción de daño del objetivo.

### `Assets/_Project/Scripts/Combat/UnitHealthBar.cs`

Barra de vida world-space formada por dos quads. Mira siempre a la cámara, ajusta el ancho del relleno según `HealthNormalized` y se oculta al morir la unidad.

### `Assets/_Project/Scripts/Combat/UnitDamageFlash.cs`

Tiñe brevemente la malla de la unidad al recibir daño usando un `MaterialPropertyBlock`, sin modificar los materiales compartidos.

### `Assets/_Project/Scripts/Buildings/BarracksBuilding.cs`

Componente del prefab de cuartel. Contiene la lista de `UnitDefinition` reclutables y un `SpawnPoint` local. `GetSpawnPosition(index)` devuelve la posición de aparición con un pequeño espaciado entre unidades consecutivas.

### `Assets/_Project/Scripts/Buildings/BuildingSelectionController.cs`

Selecciona edificios con clic izquierdo. Si el edificio pulsado es un cuartel, abre su panel de reclutamiento; si se pulsa el suelo, otro edificio o `Escape`, el panel se cierra. Ignora los clics sobre la UI y el clic que acaba de colocar un edificio.

### `Assets/_Project/Scripts/Buildings/RecruitmentManager.cs`

Guarda el cuartel seleccionado, muestra su panel de reclutamiento e instancia la unidad elegida junto a su `SpawnPoint`, inicializando su `UnitRuntime`, `UnitMovement` y `UnitCombat`. El reclutamiento es gratuito e instantáneo en esta fase.

### `Assets/_Project/Scripts/Buildings/RecruitmentPanel.cs`

Panel uGUI generado en runtime. Muestra un botón por cada unidad reclutable del cuartel activo y se oculta hasta que se construye uno. Usa `VerticalLayoutGroup` para apilar los botones y `LegacyRuntime.ttf` como fuente.

### `Assets/_Project/Scripts/Buildings/BuildingDefinition.cs`

ScriptableObject con los datos de cada edificio: categoría, prefab, huella en celdas (ancho x fondo), costes y stats de combate.

Crear assets con:

`Assets > Create > ProjectoRTS > Buildings > Building Definition`

### `Assets/_Project/Scripts/Buildings/BuildGrid.cs`

Grid lógico de construcción sobre el plano XZ. No instancia prefabs: solo valida y reserva celdas. La instanciación queda en el sistema de colocación.

API principal:

- `WorldToCell(Vector3)` / `CellToWorld(Vector2Int)`
- `CanPlace(BuildingDefinition, Vector2Int)`
- `TryPlace(BuildingDefinition, Vector2Int)`
- `TryRemove(Vector2Int)`
- `GetOccupant(Vector2Int)`
- `event Action GridChanged`

La celda `(0,0)` empieza en `origin`. `cell.y` corresponde al eje Z del mundo.

### `Assets/_Project/Scripts/Buildings/BuildingPlacementController.cs`

Gestiona el modo de colocación:

- Lanza un raycast desde la cámara al plano del suelo (con fallback a un plano matemático).
- Convierte el punto del cursor a celda y centra la huella del edificio.
- Instancia el prefab como preview no interactivo.
- Valida la huella completa con `BuildGrid.CanPlace` y colorea el preview en verde o rojo.
- Confirma con clic izquierdo y reserva las celdas con `BuildGrid.TryPlace`.
- Cancela con clic derecho o `Escape`.

### `Assets/_Project/Scripts/Buildings/PreviewGhost.cs`

Convierte el prefab instanciado en un fantasma: desactiva colliders, rigidbodies y otros `Behaviour`, apaga las sombras y aplica un material transparente compartido.

### `Assets/_Project/Scripts/Buildings/BuildingPlacementHotkeys.cs`

Atajos numéricos para probar la colocación sin UI. Las definiciones se asignan en el Inspector (hasta nueve).

### `Assets/_Project/Scripts/Editor/PlacementDemoBuilder.cs`

Herramienta de editor que genera los assets de ejemplo, prefabs, materiales y la escena de prueba. Está dividida en tres archivos parciales para mantenerla legible:

- `PlacementDemoBuilder.cs`: creación de la escena, cámara, luz, grid y NavMesh.
- `PlacementDemoBuilder.Assets.cs`: materiales, definiciones, prefabs, barras de vida y escuadra romana.
- `PlacementDemoBuilder.UI.cs`: canvas, EventSystem y panel de reclutamiento.

Menú: `ProjectoRTS > Setup > Create Placement Demo Scene`.

También se puede ejecutar en batchmode:

```text
Unity.exe -projectPath D:\ProjectoRTS -batchmode -nographics -quit -executeMethod ProjectoRTS.EditorTools.PlacementDemoBuilder.CreateDemoScene
```

## Probar la colocación

1. Abre `Assets/Scenes/PlacementDemo.unity`.
2. Pulsa Play.
3. Pulsa una tecla para elegir el edificio:
   - `1` → Greek Wall (1×1)
   - `2` → Greek Gate (2×1)
   - `3` → Greek Tower (1×1)
   - `4` → Greek House (1×1)
   - `5` → Greek Barracks (2×2)
4. Mueve el ratón: el preview se ajusta a la celda y cambia a verde (válido) o rojo (ocupado/fuera del grid).
5. Clic izquierdo para colocar. Mantén `Shift` para seguir colocando.
6. Clic derecho o `Escape` para cancelar.

La demo usa un grid de 40×40 con celdas de 1 unidad y origen `(-20, 0, -20)`, sobre un plano de 50×50. Para ver las celdas en la Scene view, selecciona el objeto `BuildGrid`: verde = libre, rojo = ocupada.

## Probar el reclutamiento

1. Abre `Assets/Scenes/PlacementDemo.unity` y pulsa Play.
2. Pulsa `5` y coloca un `Greek Barracks` (2×2).
3. Haz clic sobre el cuartel colocado: aparece el panel **Reclutamiento** en la esquina inferior izquierda.
4. Pulsa **Reclutar Hoplita arcadio** o **Reclutar Psiloi**: la unidad aparece como cápsula delante del cuartel y recibe los stats de su `UnitDefinition`.
5. Cada clic adicional en un botón genera otra unidad con un pequeño desplazamiento lateral.
6. Haz clic en el suelo u otro edificio, o pulsa `Escape`, para cerrar el panel. Vuelve a hacer clic en el cuartel para reabrirlo.

El panel se abre únicamente al seleccionar un cuartel y muestra las unidades configuradas en su `BarracksBuilding`. En esta fase no hay coste ni tiempo de entrenamiento: los campos `cost` y `trainTime` ya existen en los datos, pero se aplicarán en el paso 7 (economía).

## Probar la selección y el movimiento

1. Recluta al menos una unidad con el cuartel.
2. Haz clic izquierdo sobre una unidad: aparece un anillo cian bajo ella.
3. Mantén pulsado el botón izquierdo y arrastra para seleccionar varias unidades a la vez.
4. Haz clic derecho sobre el suelo: las unidades seleccionadas se mueven en formación y rodean los edificios.
5. Pulsa `Escape` para deseleccionar.

Las unidades usan `NavMeshAgent`, por lo que rodean los edificios colocados en lugar de atravesarlos. Si una unidad queda bloqueada por un edificio recién colocado, vuelve a darle una orden de movimiento.

## Probar el combate

La escena incluye una **Roman Test Squad** con tres legionarios romanos cerca de `(10, 10)`. Cada unidad muestra una barra de vida sobre la cabeza.

1. Recluta al menos un Hoplita arcadio o un Psiloi.
2. Selecciona la unidad y haz clic derecho sobre un legionario romano: la unidad lo perseguirá y atacará al entrar en rango.
3. También puedes mover la unidad cerca de los romanos y esperar: el combate se activa automáticamente cuando hay un enemigo dentro del rango de ataque.
4. Al recibir daño, la unidad parpadea en blanco y su barra de vida disminuye.
5. Cuando una unidad muere, su barra desaparece, se encoge, se hunde y se elimina de la selección.
6. Los legionarios también contraatacan si te acercas a su rango.

Los romanos de prueba son estáticos: todavía no patrullan ni atacan en oleadas. Eso se implementará en el paso 5.

Assets generados:

- `Assets/_Project/Data/Buildings/GreekWall.asset`
- `Assets/_Project/Data/Buildings/GreekGate.asset`
- `Assets/_Project/Data/Buildings/GreekTower.asset`
- `Assets/_Project/Data/Buildings/GreekHouse.asset`
- `Assets/_Project/Data/Buildings/GreekBarracks.asset`
- `Assets/_Project/Prefabs/Buildings/GreekWall.prefab`
- `Assets/_Project/Prefabs/Buildings/GreekGate.prefab`
- `Assets/_Project/Prefabs/Buildings/GreekTower.prefab`
- `Assets/_Project/Prefabs/Buildings/GreekHouse.prefab`
- `Assets/_Project/Prefabs/Buildings/GreekBarracks.prefab`
- `Assets/_Project/Data/Units/HopliteArcadio.asset`
- `Assets/_Project/Data/Units/Psiloi.asset`
- `Assets/_Project/Data/Units/RomanLegionary.asset`
- `Assets/_Project/Data/NavMesh/PlacementDemoNavMesh.asset`
- `Assets/_Project/Prefabs/Units/HopliteArcadio.prefab`
- `Assets/_Project/Prefabs/Units/Psiloi.prefab`
- `Assets/_Project/Prefabs/Units/RomanLegionary.prefab`
- `Assets/_Project/Materials/Ground.mat`
- `Assets/_Project/Materials/Building.mat`
- `Assets/_Project/Materials/Wall.mat`
- `Assets/_Project/Materials/Gate.mat`
- `Assets/_Project/Materials/Tower.mat`
- `Assets/_Project/Materials/Barracks.mat`
- `Assets/_Project/Materials/Hoplite.mat`
- `Assets/_Project/Materials/Psiloi.mat`
- `Assets/_Project/Materials/Roman.mat`
- `Assets/_Project/Materials/Selection.mat`
- `Assets/_Project/Materials/HealthBarBackground.mat`
- `Assets/_Project/Materials/HealthBarFill.mat`
- `Assets/_Project/Materials/PreviewValid.mat`
- `Assets/_Project/Materials/PreviewInvalid.mat`

Limitaciones actuales: no hay rotación de edificios, UI de construcción ni descuento de recursos al colocar. El grid solo valida y reserva celdas. La puerta y la torre son cubos de color sin comportamiento defensivo todavía. Las unidades ya se mueven, combaten, muestran barra de vida y mueren, pero los romanos de prueba son estáticos: todavía no hay IA enemiga ni oleadas. El reclutamiento es gratuito e instantáneo y la formación de movimiento es una rejilla simple, no la falange del paso 8.

## Roadmap del prototipo

1. [x] Terreno base + grid de construcción + colocación de muro, puerta y torre.
2. [x] Edificios de desbloqueo + sistema de unidades (`UnitDefinition`, Hoplita arcadio, Psiloi y cuartel con reclutamiento).
3. [x] Movimiento y pathfinding con NavMesh.
4. [x] Combate básico unidad vs unidad.
5. [ ] IA enemiga + spawn de romanos.
6. [ ] Sistema de oleadas + calendario día/noche.
7. [ ] Economía mínima (madera, trabajador y costes de construcción).
8. [ ] Buff de formación (falange).

## Paso 5 rediseñado: spawner unico y avance grupal

El paso 5 deja de repartir romanos por toda la escena. Ahora hay **un solo Spawn Point** en `(0, 0, 22)`, un aviso previo de 4 segundos y una escuadra que entra agrupada y marcha hacia el Panteon.

### `Assets/_Project/Scripts/Waves/RomanSpawner.cs`

- Mantiene la lista `squad` (`unit`, `count`, `delay`) y un unico `spawnPoint`.
- `BeginWave(WaveDefinition)` rellena la escuadra desde `wave.Groups` y arranca la secuencia.
- Antes de aparecer pinta un marcador pulsante en el suelo: un cilindro achatado con material creado en runtime (`Universal Render Pipeline/Unlit`, con fallback a `Unlit/Color` y `Sprites/Default`).
- Las unidades aparecen en formacion con `formationSpacing = 1.5` y `spawnStagger = 0.15`, no una a una por intervalos largos.
- Cada unidad recibe `MoveTo(destination)` al spawnear; el spawner repite la orden cada `repathInterval = 0.5` segundos **solo si la unidad no tiene objetivo de combate**.
- La IA (`EnemyAiController`) sigue activa con la patrulla desactivada: los romanos atacan casas, muros, torres y hoplitas que se crucen y retoman la marcha cuando el objetivo muere.
- Dispara `SquadArrived` cuando la escuadra termina la marcha o es eliminada.

### `Assets/_Project/Scripts/Waves/WaveManager.cs`

- Ya no instancia unidades ni usa `spawnPoints`: solo calendario, aviso y estados.
- Referencias: `calendar`, `objective` y `romanSpawner`.
- `WaveWarning` se emite una sola vez cuando la siguiente oleada es la de manana y quedan menos de `warningLeadTime` segundos de dia.
- `BeginWave` delega en `romanSpawner.BeginWave(wave)` y emite `WaveStarted`.
- `WaveCleared` llega desde `RomanSpawner.SquadArrived`; `GameOver` desde `PantheonObjective.Destroyed`, que tambien pausa el calendario.

### `Assets/_Project/Scripts/Waves/DayNightCalendar.cs`

- Expone `DayLength` para que `WaveManager` calcule el tiempo restante de dia sin duplicar la constante.

### `Assets/_Project/Scripts/Waves/EnemyAiController.cs`

- Nuevo `patrolEnabled` (por defecto `true`) y `SetPatrolEnabled(bool)`.
- `HandlePatrol()` sale de inmediato si la patrulla esta desactivada, pero la deteccion y el ataque siguen funcionando.

### Como probarlo en `PlacementDemo`

1. Abre la escena `PlacementDemo`. En la jerarquia debe haber un unico `Roman Spawner` con un solo hijo `Spawn Point` en `(0, 0, 22)`.
2. Entra en Play. El **dia 1 queda libre** para construir y reclutar.
3. Al final del dia 1 aparece el aviso de texto de la oleada del dia 2 y, en el suelo del spawn, un marcador rojo pulsante durante 4 segundos.
4. La escuadra de 4 legionarios aparece agrupada y avanza junta hacia el Panteon. Si le sale al paso una casa, un muro o un hoplita, lo ataca y luego retoma la marcha.
5. El dia 3 llegan 7 romanos y el dia 4 llegan 11.
6. Si el Panteon llega a 0 aparece `El Panteon ha caido. Derrota.` y el calendario se detiene.

Las oleadas no cambian: dia 2 = 4, dia 3 = 7, dia 4 = 11, todas con `spawnInterval = 2` y `restAfterWave = 20`.

## Camara RTS y mapa ampliado

La camara de `PlacementDemo` ya no es estatica: ahora es un rig RTS controlable con el mouse, montado sobre Cinemachine 3.1.7. El mapa paso de 52x52 a 104x104 y los romanos aparecen mas lejos del Panteon.

### `Assets/_Project/Scripts/Camera/RtsCameraController.cs`

- No mueve el Transform de la camara directamente. Mueve un `Camera Target` en el plano del suelo y cambia el `FollowOffset` del `CinemachineFollow` para el zoom; el `CinemachineBrain` de la `Main Camera` aplica el resultado.
- Paneo con **borde de pantalla**, **clic central + arrastre** y **WASD / flechas**.
- Zoom con la **rueda del mouse**, entre `minDistance = 10` y `maxDistance = 46`, suavizado.
- Limites de movimiento configurables con `SetLimits(min, max)`; el builder los fija a `-52..52` en X y Z, que es el borde del mapa de 104x104.
- API publica: `TargetPosition`, `CurrentDistance`, `MinDistance`, `MaxDistance`, `SetLimits`, `SetEdgeScrollEnabled` y `FocusOn`.

### Cambios en `PlacementDemoBuilder.cs`

- La `Main Camera` lleva ahora un `CinemachineBrain`.
- Nuevo rig `Camera Rig` con `Camera Target` y `Cinemachine Camera` (`CinemachineFollow` con offset `(0, 34, -30)`), creado por el helper `CreateRtsCamera(MapHalfSize)`.
- Constantes de mapa: `MapSize = 104`, `MapHalfSize = 52`, `GroundScale = 11.7`.
- El suelo mide `117x117` (escala `11.7` sobre el plano de 10x10) y el grid de construccion es `104x104` con origen `(-52, 0, -52)`.
- El `Spawn Point` de los romanos pasa de `(0, 0, 22)` a `(0, 0, 46)`, casi en el borde sur: la distancia al Panteon en `(0, 0, 0)` pasa de 22 a 46 unidades.

### Como probarlo

1. Regenera la escena desde el menu `ProjectoRTS > Setup > Create Placement Demo Scene` para que el rig de camara, el mapa nuevo y el spawn lejano existan.
2. Entra en Play y mueve el mouse al borde de la pantalla: la camara panea. Manten pulsado el boton central y arrastra: paneo manual. Rueda del mouse: zoom.
3. Comprueba que no puedes salirte del mapa: la camara se detiene en `-52..52` en ambos ejes.
4. Los romanos entran desde `(0, 0, 46)` y tardan bastante mas en llegar al Panteon, lo que deja mas tiempo de reaccion.

## Camara angulada con pitch

La camara RTS es una vista angulada elevada, tipo isometrica, al estilo de *Diplomacy is Not an Option*: se ve tanto el terreno como las fachadas de los edificios. Ahora tiene **rotacion libre en los dos ejes**: inclinacion vertical (pitch) y giro horizontal (yaw), ambos controlables a la vez.

### Controles

- **Paneo:** borde de pantalla, clic central + arrastre, o `WASD` / flechas. El paneo es relativo al giro actual, asi que `W` siempre aleja la vista del jugador. Limitado a `-52..52` en X y Z, el borde del mapa de `104x104`.
- **Rotacion:** **clic derecho + arrastre**. Arrastrar en horizontal gira la vista (yaw) y en vertical la inclina (pitch), de forma simultanea e independiente.
- **Rotacion con teclado:** `Q` / `E` para yaw, `PageDown` / `PageUp` para pitch.
- **Zoom:** rueda del raton, entre `14` y `70` de distancia.

### Limites

- **Pitch** acotado entre `25` y `85` grados: no puede volcarse boca abajo ni quedar mirando al cielo. El valor inicial es `50`, dentro del rango isometrico elevado de 45-60.
- **Yaw** sin limites: giro completo de 360 grados, con envoltura a `-180..180` para que el angulo no crezca sin fin.

### Implementacion con Cinemachine

- El script no toca el Transform de la `Camera`. Coloca un `Camera Target` en el plano del suelo y escribe el `FollowOffset` del `CinemachineFollow`.
- El offset se calcula en espacio de mundo: `y = d * sin(pitch)` para la altura, y `x = h * sin(yaw)`, `z = -h * cos(yaw)` para girar el eje inclinado alrededor del objetivo, con `h = d * cos(pitch)`.
- Pitch y yaw estan suavizados por separado (`rotationSmoothing`) y el zoom tambien (`zoomSmoothing`).
- API publica: `SetPitch`, `SetYaw`, `SetLimits`, `SetEdgeScrollEnabled`, `FocusOn`, `CurrentPitch`, `CurrentYaw`, `CurrentDistance`, `MinDistance`, `MaxDistance`.
- El `Pantheon` solo se usa una vez al arrancar (`initialFocus`) para fijar la posicion y el angulo iniciales. No es un `LookAt` dinamico ni un pivote de orbita.

### Como probarlo

1. Regenera la escena con `ProjectoRTS > Setup > Create Placement Demo Scene`.
2. Entra en Play: la camara arranca a 50 grados mirando al `Pantheon` desde 38 de distancia.
3. Manten pulsado el boton derecho: arrastra en horizontal para girar la vista y en vertical para inclinarla. Ambos ejes responden a la vez.
4. Suelta el boton y usa `WASD`: el movimiento sigue la direccion en la que mira la camara.
5. `Q` / `E` giran la vista y `PageUp` / `PageDown` la inclinan. La rueda hace zoom.
3. Suelta el boton y usa `WASD`: el movimiento sigue la direccion en la que mira la camara.
4. Rueda del raton para acercar y alejar. Comprueba que la camara no puede ponerse boca abajo ni salirse de `-52..52`.

## Optimización y limpieza

- `UnitRegistry` sustituye las búsquedas de unidades por toda la escena (`FindObjectsByType`) en selección, targeting y barras de vida.
- Los raycasts de selección usan un buffer reutilizado con `Physics.RaycastNonAlloc` y eligen el impacto más cercano sin ordenar un array nuevo.
- Los componentes cachean referencias (`UnitRuntime`, `UnitMovement`, `NavMeshAgent`, cámara) para evitar `GetComponent` y `Camera.main` en cada frame.
- El combate usa eventos (`HealthChanged`, `Damaged`, `Died`) en lugar de consultar la vida constantemente.
- El generador de la demo se dividió en tres archivos parciales y comparte helpers de materiales para eliminar duplicación.
- Se eliminaron los assets de plantilla `SampleScene`, `TutorialInfo` y `Readme.asset`; `PlacementDemo` es ahora la escena por defecto del proyecto y la única en Build Settings.

## Notas de reparación

- La carpeta `Library` original de la plantilla estaba corrupta y bloqueaba Package Manager. Se reconstruyó desde cero.
- AI Navigation se copió a `Packages/com.unity.ai.navigation` y se referencia como paquete local (`file:`) para que la resolución no dependa de la red.
- Las carpetas de respaldo antiguas (`Library_old`, `Library_partial`, `Temp_old`) ya se eliminaron.

## Estado actual: economía, recursos, trabajadores y muros

### Controles

- Clic izquierdo: seleccionar unidad (individual o arrastre).
- Clic derecho: mover la selección; sobre un enemigo, atacar; sobre un **muro terminado**, subir los toxotes.
- `Escape`: cancelar selección o colocación.
- Teclas `1` a `7`: iniciar colocación del edificio correspondiente (muro, puerta, torre, casa, cuartel, ayuntamiento, almacén).
- `R` durante la colocación: rotar la vista previa 90° (solo huella cuadrada; útil para orientar la rampa del muro).
- `Shift` + arrastre: pintar una línea de edificios.

### Economía

- El jugador griego empieza con 500 de madera y 300 de bronce (`PlayerEconomy`).
- Construir y reclutar descuentan recursos; los botones se deshabilitan si no alcanza.
- Costes visibles en los paneles como `M` (madera) y `B` (bronce).
- El HUD arriba a la izquierda muestra madera, bronce, trabajadores y cuántos priorizan cada mineral.

### Recursos

- `ResourceNode`: nodos finitos de madera (árboles) y bronce (rocas) repartidos en clusters por el mapa de 300x300. Se ocultan al agotarse.
- `ResourceDropOff`: puntos de depósito en Ayuntamiento, cuartel y almacén. Los trabajadores entregan en el más cercano.
- Cada trabajador carga 10 recursos y tarda 2 segundos en recolectar.

### Trabajadores

- Salen **sin prioridad** y se quedan quietos; lo único que se auto-asignan es construir (**1 trabajador por obra**).
- Selecciona trabajadores y elige **Madera** o **Bronce** en el panel derecho para que empiecen a recolectar ese mineral (verde = activo).
- Cambiar la prioridad cancela la recolección actual al instante (si no llevan carga).
- Huyen de enemigos cercanos y vuelven a su tarea cuando el área queda despejada.

### Muros, torres y arqueros

- El muro tiene rampa y almenas; la rotación con `R` permite dejar la rampa mirando hacia la base.
- `WallMountPoint`: cada tramo admite 2 toxotes. Clic derecho sobre un muro terminado con toxotes seleccionados los sube; clic derecho sobre otro tramo cercano los traslada por arriba de la muralla.
- Montados ganan +2 de alcance; clic derecho en suelo o enemigo los desmonta.
- `BuildingRangedAttack`: las torres disparan flechas visibles a enemigos en rango (10 de daño, alcance 9). El muro en sí no dispara.
- Los toxotes (psiloi) tienen alcance 9.

### Edificios

| Edificio | Tecla | Coste | Notas |
|---|---|---|---|
| Muro | 1 | 10M 5B | 2 puestos de toxotes, rampa |
| Puerta | 2 | 30M 20B | huella 2x1, no rota |
| Torre | 3 | 30M 40B | ataque a distancia automático |
| Casa | 4 | 50M 20B | decorativa |
| Cuartel | 5 | 100M 50B | recluta hoplitas y toxotes |
| Ayuntamiento | 6 | 125M 75B | recluta trabajadores, depósito |
| Almacén | 7 | 75M 10B | depósito de recursos |

### Regenerar la escena

Tras cambios en el generador, corre el menú `ProjectoRTS > Setup > Create Placement Demo Scene`. Esto regenera terreno, NavMesh, cámara, edificios, nodos de recursos y oleadas; las UI de runtime (HUD, panel de construcción, panel de prioridad) se crean solas al entrar en Play.

## Siguiente paso

1. Equilibrar oleadas y economía (población, límites, velocidades).
2. Falange (paso 8 del roadmap) y mejoras de combate.
3. Movimiento verdaderamente libre de arqueros sobre muros (pasarelas + pathfinding propio).
