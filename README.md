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
- Colocación de edificios: **implementada** con preview, raycast, snap al grid y validación de celdas.
- Escena de prueba: `Assets/Scenes/PlacementDemo.unity`.
- Paso 1 del roadmap: **completado** (terreno, grid y colocación de muro, puerta, torre, casa y cuartel griego).
- Paso 2 del roadmap: **completado** (unidades Hoplita arcadio y Psiloi, cuartel con reclutamiento y panel uGUI).
- Paso 3 del roadmap: **completado** (NavMesh horneado, selección con clic/arrastre y movimiento con clic derecho).
- Paso 4 del roadmap: **completado** (combate automático y por orden, barras de vida, feedback de daño y muerte).
- Los edificios del jugador son griegos; los romanos llegan como escuadra de prueba y se convertirán en oleadas enemigas.

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

## Siguiente paso: 5. IA enemiga + spawn de romanos

1. Dar a los legionarios un comportamiento de patrulla y detección de unidades griegas cercanas.
2. Hacer que persigan y ataquen objetivos dentro de un radio de agresión.
3. Añadir un spawner de romanos con puntos de aparición y límite de unidades.
4. Conectar la IA con el sistema de oleadas del paso 6.
5. Mantener el combate actual como base para la falange del paso 8.
