# DeliveryRoutes — Taller 5: Archivos Planos y Grafos

Programa de consola en C# / .NET que lee el mapa de una ciudad (`city.txt`) y la lista de
recogidas y entregas (`packages.txt`) y arma la ruta más corta que encuentra para un domiciliario.

## Cómo ejecutar

Requiere el SDK de .NET 10 (si usa otra versión, cambie `TargetFramework` en `DeliveryRoutes.csproj`).

```bash
dotnet build
dotnet run -- city.txt packages.txt D            # caso completo, arranca en D
dotnet run -- city_mini.txt packages_mini.txt A  # ejemplo pequeño
dotnet run                                       # sin argumentos: pide rutas y posición inicial
```

## Estrategia

**Parte exacta: distancias.** El mapa es un grafo dirigido y ponderado (lista de adyacencia):
una calle `TwoWay` genera dos aristas y una `OneWay` genera una sola. No hace falta conocer la
distancia entre las 100 ubicaciones, solo entre la posición inicial y los puntos de recogida y
entrega (máximo 61). Se ejecuta Dijkstra (con `PriorityQueue`) una vez desde cada uno y se guarda
una tabla `distance[x][y]` y los predecesores para reconstruir la línea `Camino:`. BFS no sirve
porque cuenta calles y no metros. Como el grafo es dirigido, `distance[x][y]` y `distance[y][x]`
pueden ser distintas.

**Parte heurística: el orden de las 60 paradas.** Probar todas las órdenes válidas es imposible:
60! / 2^30 tiene 73 cifras. Este es el problema de recogida y entrega (Pickup and Delivery
Problem), una variante del agente viajero. Por eso se usa una heurística, que no garantiza el
óptimo pero da buenas rutas en pocos segundos:

1. **Inserción más barata:** los paquetes se meten uno a uno. Para cada uno se prueban todas las
   posiciones de su recogida y de su entrega (la recogida siempre antes que la entrega) y se elige
   la que menos alarga la ruta.
2. **Reubicación de paquetes:** se saca la recogida y la entrega de un paquete y se reinsertan en
   su mejor posición, mientras la ruta siga mejorando.
3. **Mover una parada:** se saca una sola parada y se prueba en otras posiciones que respeten
   "recoger antes de entregar".
4. **Reinicios aleatorios:** como el resultado depende del orden en que se insertan los paquetes,
   se repite todo 200 veces con órdenes aleatorios (`Random` con semilla fija, así el resultado es
   reproducible) y se conserva la mejor ruta.

Al final se verifica que cada paquete se recoja una vez y se entregue una vez después de recogerlo,
y que el total impreso sea igual a la suma de los tramos.

## Resultados

| Estrategia | Recorrido total |
|---|---|
| Vecino más cercano (sin mejoras) | 40.52 km |
| Inserción + reubicación + mover parada + reinicios | 30.39 km |

## Estructura

```
Program.cs                 Lee argumentos o consola, llama al backend, imprime
Backend/Edge.cs, Graph.cs  Grafo dirigido con lista de adyacencia
Backend/CityFile.cs        Carga y valida city.txt (TwoWay = 2 aristas, OneWay = 1)
Backend/PackageFile.cs     Carga y valida packages.txt, agrupa por paquete
Backend/ShortestPaths.cs   Dijkstra y reconstrucción del camino
Backend/RoutePlanner.cs    Vecino más cercano + inserción + mejoras
Backend/RoutePrinter.cs    Arma la salida de la sección 5
```

## Salida del ejemplo pequeño (`city_mini.txt`, `packages_mini.txt`, inicio `A`)

```
<<PEGAR AQUÍ el contenido de salida_mini.txt>>
```

## Salida completa (`city.txt`, `packages.txt`, inicio `D`)

```
<<PEGAR AQUÍ el contenido de salida_completa.txt>>
```
