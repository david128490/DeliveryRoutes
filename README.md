# DeliveryRoutes — Taller 5: Archivos Planos y Grafos

Programa de consola en C# / .NET 8 que lee el mapa de una ciudad (`city.txt`) y la lista de
recogidas y entregas (`packages.txt`) y arma la ruta más corta que encuentra para un domiciliario.

## Cómo ejecutar

```bash
dotnet build
dotnet run -- city.txt packages.txt D          # caso completo, arranca en D
dotnet run -- city_mini.txt packages_mini.txt A # ejemplo pequeño
dotnet run                                      # sin argumentos: pide rutas y posición inicial
```

## Estrategia (explicación)

**Parte exacta: distancias.** El mapa es un grafo *dirigido* y ponderado (lista de adyacencia): una
calle `TwoWay` genera dos aristas y una `OneWay` genera una sola. No hace falta conocer la distancia
entre las 100 ubicaciones, solo entre la posición inicial y los puntos de recogida/entrega (máx. 61).
Se ejecuta **Dijkstra** (con `PriorityQueue`) una vez desde cada uno y se guarda una tabla
`distance[x][y]` y los predecesores para reconstruir el `Camino:`. BFS no sirve porque cuenta calles,
no metros. Como el grafo es dirigido, `distance[x][y]` y `distance[y][x]` pueden ser distintas.

**Parte heurística: el orden de las 60 paradas.** Probar todas las órdenes válidas es imposible:
60! / 2^30 tiene 73 cifras. Es el *Pickup and Delivery Problem* (variante del agente viajero), así
que se usa una heurística que no garantiza el óptimo pero da buenas rutas en pocos segundos:

1. **Inserción más barata:** los paquetes se meten uno a uno; para cada uno se prueban todas las
   posiciones de su recogida y de su entrega (recogida siempre antes que la entrega) y se elige la que
   menos alarga la ruta. El aumento de costo se calcula en O(1) con la tabla de distancias.
2. **Reubicación de paquetes:** se saca la recogida y la entrega de un paquete y se reinsertan en su
   mejor posición, mientras la ruta siga mejorando.
3. **Mover una parada:** se saca una sola parada y se prueba en otras posiciones que respeten
   "recoger antes de entregar".
4. **Reinicios aleatorios:** como el resultado depende del orden en que se insertan los paquetes, se
   repite todo 200 veces con órdenes aleatorios (`Random` con semilla fija 12345 → reproducible) y se
   conserva la mejor ruta.

Al final `IsValid` verifica que cada paquete se recoja una vez y se entregue una vez después de
recogerlo, y el total impreso se compara con la suma de los tramos.

## Estructura

```
Program.cs                 Lee argumentos/consola, llama al backend, imprime
Backend/Edge.cs, Graph.cs  Grafo dirigido con lista de adyacencia
Backend/CityFile.cs        Carga y valida city.txt (TwoWay = 2 aristas, OneWay = 1)
Backend/PackageFile.cs     Carga y valida packages.txt, agrupa por paquete
Backend/ShortestPaths.cs   Dijkstra y reconstrucción del camino
Backend/RoutePlanner.cs    Voraz (vecino más cercano) + inserción + mejoras
Backend/RoutePrinter.cs    Arma la salida de la sección 5
```

## Salida del ejemplo pequeño (sección 6)

> Nota: hay dos rutas empatadas en 2.44 km; esta es una de ellas (la del enunciado es la otra).

```
Posición inicial: A
Paquetes: 3 Ubicaciones: 7 Calles: 7 (1 de un solo sentido)

1. Ir de A a C (400 m): recoger paquete 1.
   Camino: A -> B -> C
2. Ir de C a G (620 m): entregar paquete 1 y recoger paquete 3.
   Camino: C -> B -> E -> F -> G
3. Ir de G a E (270 m): recoger paquete 2.
   Camino: G -> F -> E
4. Ir de E a A (550 m): entregar paquete 3.
   Camino: E -> B -> A
5. Ir de A a D (600 m): entregar paquete 2.
   Camino: A -> B -> C -> D

Todos los paquetes fueron recogidos y entregados.
Recorrido total: 2.44 km
```

## Salida completa: `city.txt`, `packages.txt`, posición inicial `D`

> **IMPORTANTE:** esta salida fue generada con un prototipo en Python de la misma lógica porque el
> entorno donde se escribió no tenía .NET. **Reemplácela por la salida real de
> `dotnet run -- city.txt packages.txt D` en su máquina** antes de entregar.

```
Posición inicial: D
Paquetes: 30 Ubicaciones: 100 Calles: 142 (30 de un solo sentido)

1. En D: recoger paquete 18.
2. Ir de D a AR (1110 m): recoger paquete 20.
   Camino: D -> BT -> P -> CB -> AD -> AR
3. Ir de AR a BB (150 m): recoger paquete 16.
   Camino: AR -> BB
4. Ir de BB a AJ (400 m): recoger paquetes 26 y 23.
   Camino: BB -> AJ
5. Ir de AJ a BE (1660 m): recoger paquete 7.
   Camino: AJ -> BB -> AR -> AD -> CB -> P -> BT -> BE
6. Ir de BE a X (490 m): recoger paquete 12.
   Camino: BE -> E -> X
7. Ir de X a BU (560 m): recoger paquete 8.
   Camino: X -> B -> BU
8. Ir de BU a CN (430 m): recoger paquetes 1, 2 y 3.
   Camino: BU -> CN
9. Ir de CN a BU (430 m): recoger paquete 28.
   Camino: CN -> BU
10. Ir de BU a AM (350 m): recoger paquete 30.
   Camino: BU -> AM
11. Ir de AM a AX (270 m): recoger paquete 17.
   Camino: AM -> AK -> AX
12. Ir de AX a AQ (170 m): recoger paquete 9.
   Camino: AX -> AQ
13. Ir de AQ a CS (560 m): recoger paquete 19.
   Camino: AQ -> BS -> CS
14. Ir de CS a BS (310 m): recoger paquete 25.
   Camino: CS -> BS
15. Ir de BS a CR (520 m): entregar paquete 20.
   Camino: BS -> CT -> CR
16. Ir de CR a CH (680 m): recoger paquete 10.
   Camino: CR -> BO -> CJ -> CH
17. Ir de CH a CU (1050 m): recoger paquete 15.
   Camino: CH -> CJ -> CV -> BX -> CU
18. Ir de CU a M (1060 m): entregar paquete 1.
   Camino: CU -> CV -> W -> CM -> M
19. Ir de M a AE (430 m): entregar paquete 26.
   Camino: M -> CM -> AE
20. Ir de AE a BW (2130 m): entregar paquete 30.
   Camino: AE -> AP -> AM -> AK -> S -> K -> N -> BW
21. Ir de BW a R (630 m): recoger paquetes 6 y 21.
   Camino: BW -> CL -> R
22. Ir de R a CL (360 m): recoger paquete 14.
   Camino: R -> CL
23. Ir de CL a BN (450 m): entregar paquete 7.
   Camino: CL -> BN
24. Ir de BN a AI (1130 m): recoger paquete 24.
   Camino: BN -> Y -> AV -> CG -> AI
25. Ir de AI a AO (170 m): entregar paquete 3 y recoger paquete 11.
   Camino: AI -> AO
26. Ir de AO a BM (720 m): entregar paquetes 10 y 2.
   Camino: AO -> AI -> AS -> BM
27. Ir de BM a BI (1750 m): recoger paquete 13.
   Camino: BM -> AS -> BH -> BL -> CO -> BI
28. Ir de BI a BY (990 m): entregar paquete 16.
   Camino: BI -> CO -> AZ -> V -> O -> BY
29. Ir de BY a Q (390 m): recoger paquetes 29 y 22.
   Camino: BY -> Q
30. Ir de Q a AA (1450 m): entregar paquete 12.
   Camino: Q -> BJ -> CI -> Z -> AA
31. Ir de AA a AC (900 m): entregar paquete 17.
   Camino: AA -> Z -> CI -> AC
32. Ir de AC a CD (130 m): entregar paquete 25.
   Camino: AC -> CD
33. Ir de CD a BF (710 m): entregar paquetes 18 y 23.
   Camino: CD -> BA -> AG -> BF
34. Ir de BF a AP (760 m): entregar paquetes 11 y 22 y recoger paquete 5.
   Camino: BF -> X -> B -> AP
35. Ir de AP a AT (350 m): entregar paquetes 15 y 14 y recoger paquete 27.
   Camino: AP -> AT
36. Ir de AT a BV (1000 m): entregar paquete 19.
   Camino: AT -> AQ -> AX -> F -> BV
37. Ir de BV a AB (470 m): recoger paquete 4.
   Camino: BV -> AB
38. Ir de AB a AQ (1270 m): entregar paquete 6.
   Camino: AB -> BV -> F -> AX -> AQ
39. Ir de AQ a CT (350 m): entregar paquete 4.
   Camino: AQ -> BS -> CT
40. Ir de CT a CV (600 m): entregar paquete 28.
   Camino: CT -> BK -> CV
41. Ir de CV a T (820 m): entregar paquetes 24 y 27.
   Camino: CV -> W -> CF -> T
42. Ir de T a BE (440 m): entregar paquete 21.
   Camino: T -> BE
43. Ir de BE a CP (460 m): entregar paquete 9.
   Camino: BE -> BT -> CP
44. Ir de CP a AD (670 m): entregar paquetes 5 y 13.
   Camino: CP -> BT -> P -> CB -> AD
45. Ir de AD a AR (280 m): entregar paquete 8.
   Camino: AD -> AR
46. Ir de AR a AJ (550 m): entregar paquete 29.
   Camino: AR -> BB -> AJ

Todos los paquetes fueron recogidos y entregados.
Recorrido total: 30.56 km```
