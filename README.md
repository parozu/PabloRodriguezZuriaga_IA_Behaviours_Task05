# IA Behaviours - Task 05

Práctica de la asignatura de Inteligencia Artificial. Un NPC ladrón (Robber) se mueve de forma autónoma con distintos comportamientos mientras el jugador controla al policía (Cop). El comportamiento activo se elige con el teclado.

Autor: Pablo Rodríguez Zuriaga
Plantilla base: [IA_Behaviours_Template](https://github.com/jjorgeProjects/IA_Behaviours_Template)

## Requisitos

- Unity 6000.3.16f1 (Unity 6.3), URP
- Paquete AI Navigation (NavMeshSurface)
- Active Input Handling en **Both**: el proyecto usa el Input Manager antiguo (`Input.GetKeyDown`, `Input.GetAxis`)

## Cómo probarlo

1. Abrir el proyecto con Unity 6.3.
2. Abrir la escena `Assets/Scenes/Steering.unity`.
3. Pulsar Play. Para ver los rayos y gizmos de depuración, tener la vista Scene abierta y seleccionar un Robber.

También hay un ejecutable de Windows en la sección Releases del repositorio.

## Controles

| Tecla | Acción |
|---|---|
| W / S | Mover al Cop adelante / atrás |
| A / D | Girar al Cop |
| H | Hide |
| P | Pursue |
| E | Evade |
| R | Wander |
| C | Complex |

Las teclas cambian el estado de todos los Robbers de la escena a la vez.

## Comportamientos

Todo el comportamiento está en `Assets/Scripts/Bot.cs`. El movimiento usa `NavMeshAgent`: cada comportamiento calcula un punto de destino y el agente se encarga de llegar a él respetando los límites del NavMesh y los obstáculos.

### Seek y Flee

Comportamientos base que usan los demás.

- **Seek**: el destino es la posición indicada.
- **Flee**: el destino es un punto a `fleeDistance` metros en la dirección contraria a la posición indicada. Se corrige con `NavMesh.SamplePosition` para que no caiga fuera del NavMesh cerca de los bordes.

### Pursue (P)

Predice dónde estará el Cop y va hacia ese punto:

```
futurePos = copPos + copVelocity * t
t = distancia / (velocidad del Robber + velocidad del Cop)
```

La velocidad del Cop se calcula en `Bot.cs` a partir de su desplazamiento entre frames, sin depender de `Drive.cs`. El tiempo de predicción está limitado por `maxPredictionTime`. Si el Cop está parado, va directamente a su posición.

### Evade (E)

Igual que Pursue, pero aplica Flee sobre la posición predicha del Cop.

### Wander (R)

Movimiento aleatorio suave basado en un círculo imaginario delante del NPC:

- `wanderDistance`: distancia del NPC al centro del círculo.
- `wanderRadius`: radio del círculo.
- `wanderJitter`: cuánto se desplaza al azar cada frame el punto objetivo sobre el círculo.

El punto objetivo (`wanderTarget`) se guarda entre frames y se mueve poco a poco, así que la dirección cambia de forma gradual y no a saltos.

### Hide (H)

Busca el escondite más cercano detrás de un obstáculo, en el lado contrario al Cop. Funciona con obstáculos de cualquier tamaño:

1. Calcula la dirección Cop → obstáculo.
2. Se sitúa muy por detrás del obstáculo, a una distancia mayor que su tamaño.
3. Lanza un rayo de vuelta contra el collider del obstáculo (`Collider.Raycast`) para encontrar su cara trasera.
4. El escondite queda a `hideMargin` metros de esa cara.

Los obstáculos se obtienen de la clase `World`, que recoge los objetos con la tag `hide`. Para evitar que el NPC dude entre dos escondites a la misma distancia, el escondite actual tiene preferencia (`spotSwitchBias`).

### Complex (C)

Máquina de estados interna con tres subestados:

| Subestado | Comportamiento | Transición |
|---|---|---|
| Wandering | Wander | Pasa a Approaching si el Cop está en rango y hay línea de visión |
| Approaching | Pursue | Pasa a Hiding si el Cop le ve; vuelve a Wandering si el Cop se aleja |
| Hiding | Hide (Evade si no hay escondite) | Tras `hideTime` segundos sin ser visto, vuelve a Approaching o Wandering |

Para evitar que el NPC cambie de estado continuamente ("comportamiento nervioso") se usan tres técnicas:

- **Memoria**: `lastSeenTime` guarda la última vez que el Cop le vio. No vuelve a acercarse hasta que pasan `hideTime` segundos desde entonces.
- **Histéresis de distancia**: entra en rango a `detectionRange` metros, pero no sale hasta `detectionRange * exitRangeMultiplier`.
- **Histéresis de escondite**: `spotSwitchBias`, descrita en Hide.

## Percepción

| Función | Qué comprueba |
|---|---|
| `IsTargetInRange()` | El Cop está a menos de `detectionRange` metros |
| `CanTargetSeeMe()` | El Robber está dentro del cono de visión del Cop (`visionAngle`, 60° en total) y no hay obstáculos entre ambos |
| `CanSeeTarget()` | No hay obstáculos entre el Robber y el Cop |

La línea de visión se comprueba con `Physics.Linecast` y la máscara `obstacleMask`. Esa máscara excluye las capas `Cop` y `Robber`, así que los personajes no se tapan entre sí y los colliders de sus piezas no interfieren.

## Arquitectura

El Robber usa dos niveles de máquina de estados:

- **Estado general** (`State`): Idle, Hide, Pursue, Evade, Wander, Complex. Lo cambia el teclado en `ReadInput()` y lo ejecuta un `switch` en `Update()`.
- **Subestado de Complex** (`ComplexState`): Wandering, Approaching, Hiding. Lo decide el propio NPC según su percepción.

Las teclas solo cambian el estado. El comportamiento correspondiente se ejecuta en cada frame mientras el estado esté activo.

## Configuración de la escena

- **Tag `hide`**: obstáculos donde esconderse (4 cubos de distintos tamaños y un árbol). Todos necesitan un collider.
- **Capa `Cop`**: el policía y sus hijos.
- **Capa `Robber`**: el prefab del ladrón y sus hijos.
- **NavMeshSurface** en el objeto `Environment`. Si se mueven o añaden obstáculos, hay que volver a hornearlo.
- **Varios NPCs**: hay 3 instancias del prefab `Robber`, cada una con el Cop asignado como `target`. Cada instancia tiene su propio estado y su propia memoria.

## Parámetros (Inspector del Robber)

| Parámetro | Valor | Descripción |
|---|---|---|
| `fleeDistance` | 10 | Distancia a la que huye en cada cálculo |
| `maxPredictionTime` | 3 | Límite de predicción en Pursue / Evade (s) |
| `wanderRadius` | 5 | Radio del círculo de Wander |
| `wanderDistance` | 8 | Distancia al centro del círculo de Wander |
| `wanderJitter` | 1 | Variación aleatoria por frame en Wander |
| `hideMargin` | 1.5 | Separación respecto a la cara trasera del obstáculo |
| `visionAngle` | 60 | Cono de visión total del Cop (°) |
| `detectionRange` | 15 | Distancia de interés del Robber |
| `hideTime` | 4 | Tiempo que espera escondido antes de volver (s) |
| `exitRangeMultiplier` | 1.3 | Histéresis de distancia |
| `spotSwitchBias` | 2 | Preferencia por el escondite actual (m) |

## Depuración

En la vista Scene, con un Robber seleccionado:

- Esfera amarilla: `detectionRange`.
- Líneas cian: bordes del cono de visión del Cop.
- Círculo y punto amarillos: círculo de Wander y punto objetivo.
- Línea roja / verde: línea de visión libre / bloqueada.
- Línea magenta: posición predicha del Cop.
- Línea azul: escondite elegido.

Además, en la consola se registra cada cambio de estado y de subestado.

## Limitaciones conocidas

- El jitter de Wander se aplica una vez por frame, así que depende de los FPS.
- Al terminar `hideTime`, el Robber sale del escondite aunque el Cop siga mirando hacia él, porque el obstáculo le impide saberlo.
- Las líneas de depuración y los gizmos solo se ven en el editor, no en el ejecutable.
