# DeliveryRoutes — Taller 5: Archivos Planos y Grafos

Programa de consola en C# / .NET que lee el mapa de una ciudad (`city.txt`) y la lista de
recogidas y entregas (`packages.txt`) y arma la ruta más corta que encuentra para un domiciliario
que recoge y entrega 30 paquetes.

## Cómo ejecutar

Requiere el SDK de .NET. El proyecto usa `net10.0`; si tiene otra versión instalada, cambie
`TargetFramework` en `DeliveryRoutes.csproj`.

```bash
dotnet build
dotnet run -- city.txt packages.txt D            # caso completo, arranca en D
dotnet run -- city_mini.txt packages_mini.txt A  # ejemplo pequeño
dotnet run                                       # sin argumentos: pide rutas y posición inicial
```

## Cómo funciona

### 1. Lectura y validación de archivos

`CityFile` y `PackageFile` ignoran las líneas en blanco y los comentarios (`#`), y reportan todos
los errores juntos, cada uno con su número de línea: formato incorrecto, distancia no positiva,
sentido distinto de `TwoWay`/`OneWay`, calle repetida, ubicación inexistente, acción inválida,
paquete con dos recogidas, dos entregas o con alguna faltante. Además, antes de calcular, se
comprueba que todos los puntos sean alcanzables entre sí respetando el sentido de las calles.

### 2. El mapa como grafo dirigido

Cada ubicación es un vértice y cada sentido en que se puede recorrer una calle es una arista con
su distancia en metros. Una calle `TwoWay` crea dos aristas (una por sentido) y una `OneWay` crea
solo una. Se guarda como lista de adyacencia. Por eso la distancia de `X` a `Y` puede ser distinta
de la de `Y` a `X`: por ejemplo, de `AQ` a `BS` hay 250 m, pero de `BS` a `AQ` hay 1630 m.

### 3. Distancias exactas con Dijkstra

No hace falta conocer la distancia entre las 100 ubicaciones, solo entre la posición inicial y los
puntos de recogida y entrega. Se ejecuta Dijkstra (con `PriorityQueue`) desde cada uno de ellos y
se guarda una tabla de distancias mínimas y los predecesores, que sirven para reconstruir las
calles de la línea `Camino:`. No se usa BFS porque cuenta calles, no metros, y el camino con menos
calles no siempre es el más corto.

### 4. Por qué no se prueban todas las combinaciones

Con 30 paquetes hay 60 paradas. Aun respetando que cada paquete se recoge antes de entregarse,
quedan 60! / 2^30 órdenes posibles, un número de 73 cifras: ningún computador puede revisarlas.
Este es el problema de recogida y entrega (Pickup and Delivery Problem), una variante del
agente viajero, y no se conoce un algoritmo exacto y rápido para tamaños grandes. Por eso se usa
una heurística: no garantiza el óptimo, pero da rutas muy buenas en pocos segundos.

### 5. Estrategia para ordenar las paradas

1. **Inserción más barata.** Se arma la ruta paquete por paquete. Para cada paquete se prueban todas
   las posiciones posibles de su recogida y de su entrega (la recogida siempre antes que la
   entrega) y se elige la que menos alarga la ruta. El aumento se calcula con la tabla de
   distancias, sin recorrer toda la ruta.
2. **Reubicación de paquetes.** Se saca la recogida y la entrega de un paquete y se vuelven a
   insertar en su mejor posición. Se repite con todos los paquetes mientras la ruta mejore.
3. **Mover una parada.** Se saca una sola parada y se prueba en otras posiciones, siempre que se siga
   cumpliendo "recoger antes de entregar".
4. **Reinicios aleatorios.** El resultado depende del orden en que se insertan los paquetes, así
   que todo se repite 200 veces con órdenes aleatorios (con una semilla fija, para que el
   resultado sea reproducible) y se conserva la mejor ruta.

Al final se verifica que cada paquete se recoja una vez y se entregue una vez después de
recogerlo, y que el total impreso sea igual a la suma de los tramos.

## Resultados

| Estrategia | Recorrido total |
|---|---|
| Vecino más cercano, sin mejoras | 40.52 km |
| Inserción + reubicación + mover parada + reinicios (la usada en el programa) | 30.39 km |

El ejemplo pequeño da 2.44 km, el óptimo conocido. En ese caso hay dos rutas distintas con el
mismo total, así que el orden de las paradas puede diferir del enunciado.

## Estructura del proyecto

```
Program.cs                     Lee argumentos o consola, llama al backend e imprime
Backend/Edge.cs, Graph.cs      Grafo dirigido con lista de adyacencia
Backend/CityFile.cs            Carga y valida city.txt (TwoWay = 2 aristas, OneWay = 1)
Backend/PackageFile.cs         Carga y valida packages.txt y agrupa por paquete
Backend/Package.cs, Stop.cs    Paquete y parada de la ruta
Backend/ShortestPaths.cs       Dijkstra y reconstrucción del camino
Backend/RoutePlanner.cs        Vecino más cercano + inserción + mejoras
Backend/RoutePrinter.cs        Arma la salida paso a paso
Backend/DataValidationException.cs   Errores de validación con su número de línea
```

## Salida del ejemplo pequeño (`city_mini.txt`, `packages_mini.txt`, inicio `A`)

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

## Salida completa (`city.txt`, `packages.txt`, inicio `D`)

```
 Posición inicial: D
Paquetes: 30 Ubicaciones: 100 Calles: 142 (30 de un solo sentido)

1. En D: recoger paquete 18.
2. Ir de D a AR (1110 m): recoger paquete 20.
   Camino: D -> BT -> P -> CB -> AD -> AR
3. Ir de AR a AJ (550 m): recoger paquetes 26 y 23.
   Camino: AR -> BB -> AJ
4. Ir de AJ a BB (400 m): recoger paquete 16.
   Camino: AJ -> BB
5. Ir de BB a BE (1260 m): recoger paquete 7.
   Camino: BB -> AR -> AD -> CB -> P -> BT -> BE
6. Ir de BE a X (490 m): recoger paquete 12.
   Camino: BE -> E -> X
7. Ir de X a BU (560 m): recoger paquete 8.
   Camino: X -> B -> BU
8. Ir de BU a CN (430 m): recoger paquetes 1, 3 y 2.
   Camino: BU -> CN
9. Ir de CN a BU (430 m): recoger paquete 28.
   Camino: CN -> BU
10. Ir de BU a AX (620 m): recoger paquete 17.
   Camino: BU -> AM -> AK -> AX
11. Ir de AX a AQ (170 m): recoger paquete 9.
   Camino: AX -> AQ
12. Ir de AQ a CS (560 m): recoger paquete 19.
   Camino: AQ -> BS -> CS
13. Ir de CS a BS (310 m): recoger paquete 25.
   Camino: CS -> BS
14. Ir de BS a CR (520 m): entregar paquete 20.
   Camino: BS -> CT -> CR
15. Ir de CR a CH (680 m): recoger paquete 10.
   Camino: CR -> BO -> CJ -> CH
16. Ir de CH a CU (1050 m): recoger paquete 15.
   Camino: CH -> CJ -> CV -> BX -> CU
17. Ir de CU a CV (360 m): entregar paquete 28.
   Camino: CU -> CV
18. Ir de CV a M (700 m): entregar paquete 1.
   Camino: CV -> W -> CM -> M
19. Ir de M a AE (430 m): entregar paquete 26.
   Camino: M -> CM -> AE
20. Ir de AE a AP (370 m): recoger paquete 5.
   Camino: AE -> AP
21. Ir de AP a AM (390 m): recoger paquete 30.
   Camino: AP -> AM
22. Ir de AM a BV (900 m): entregar paquete 19.
   Camino: AM -> AK -> AX -> F -> BV
23. Ir de BV a AB (470 m): recoger paquete 4.
   Camino: BV -> AB
24. Ir de AB a BW (2360 m): entregar paquete 30.
   Camino: AB -> BV -> F -> AX -> S -> K -> N -> BW
25. Ir de BW a CL (270 m): recoger paquete 14.
   Camino: BW -> CL
26. Ir de CL a R (360 m): recoger paquetes 6 y 21.
   Camino: CL -> R
27. Ir de R a BN (810 m): entregar paquete 7.
   Camino: R -> CL -> BN
28. Ir de BN a BM (1600 m): entregar paquetes 2 y 10.
   Camino: BN -> Y -> AV -> CG -> AS -> BM
29. Ir de BM a AO (720 m): entregar paquete 3 y recoger paquete 11.
   Camino: BM -> AS -> AI -> AO
30. Ir de AO a AI (170 m): recoger paquete 24.
   Camino: AO -> AI
31. Ir de AI a BI (1290 m): recoger paquete 13.
   Camino: AI -> CG -> BH -> BL -> CO -> BI
32. Ir de BI a BY (990 m): entregar paquete 16.
   Camino: BI -> CO -> AZ -> V -> O -> BY
33. Ir de BY a Q (390 m): recoger paquetes 29 y 22.
   Camino: BY -> Q
34. Ir de Q a AA (1450 m): entregar paquete 12.
   Camino: Q -> BJ -> CI -> Z -> AA
35. Ir de AA a AC (900 m): entregar paquete 17.
   Camino: AA -> Z -> CI -> AC
36. Ir de AC a CD (130 m): entregar paquete 25.
   Camino: AC -> CD
37. Ir de CD a BF (710 m): entregar paquetes 18 y 23.
   Camino: CD -> BA -> AG -> BF
38. Ir de BF a AP (760 m): entregar paquetes 22 y 11.
   Camino: BF -> X -> B -> AP
39. Ir de AP a AT (350 m): entregar paquetes 14 y 15 y recoger paquete 27.
   Camino: AP -> AT
40. Ir de AT a AQ (200 m): entregar paquete 6.
   Camino: AT -> AQ
41. Ir de AQ a CT (350 m): entregar paquete 4.
   Camino: AQ -> BS -> CT
42. Ir de CT a T (1420 m): entregar paquetes 27 y 24.
   Camino: CT -> BK -> CV -> W -> CF -> T
43. Ir de T a BE (440 m): entregar paquete 21.
   Camino: T -> BE
44. Ir de BE a CP (460 m): entregar paquete 9.
   Camino: BE -> BT -> CP
45. Ir de CP a AD (670 m): entregar paquetes 13 y 5.
   Camino: CP -> BT -> P -> CB -> AD
46. Ir de AD a AR (280 m): entregar paquete 8.
   Camino: AD -> AR
47. Ir de AR a AJ (550 m): entregar paquete 29.
   Camino: AR -> BB -> AJ

Todos los paquetes fueron recogidos y entregados.
Recorrido total: 30.39 km

```
