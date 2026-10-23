# SafeBand · Checklist para terminar el proyecto

> Entrega final: finales de noviembre de 2026. Se marca con `[x]` lo terminado.
> Orden recomendado: Ubicación → Escuela (grupos y maestros) → Alertas → Nube → Terminales → App → Pruebas y entrega.
> Si el tiempo aprieta, los primeros candidatos a "trabajo futuro" son: pase de lista, tienda y notificaciones push.

## Ubicación
- [x] 1. Subir la potencia de la pulsera a +20 dBm (−70 → −63 dBm a 2–2.5 m)
- [x] 2. Prueba de alcance: RSSI a varias distancias → `RSSI_1m` y `n` (pedido del profesor) — libre: RSSI_1m −76.1 dBm, n 1.83
- [x] 3. Resaltar la zona del nodo con la señal más fuerte (que no salte entre zonas) — probado con 3 nodos en la nube
- [ ] 4. Posición X, Y de los nodos + cálculo de triangulación en la API
- [ ] 4b. Ubicación híbrida: cada zona elige **por nodo** (salones, presencia) o **por triangulación** (pasillos, patios, entradas); zona según el punto; cuartos chicos sin nodo → "Cerca de…". Agrupar cubículos chicos en el plano
- [x] 5. Punto con círculo de incertidumbre en el plano de "Mis hijos"
- [ ] 5b. **Modo búsqueda** (RF-23): punto triangulado en vivo cada 1–2 s. Lo activan administradores, maestros y padres (su hijo) desde la página, y se activa solo con un SOS. Dura hasta que alguien lo desactive
- [ ] 5c. (Opcional) Durante la búsqueda, la pulsera anuncia cada 100 ms (requiere canal BLE nodo → pulsera)

## Pulsera (hardware)
- [ ] 6. Conectar y probar reed switch + imán, botón SOS, LED WS2812 y motor con transistor
- [ ] 7. Divisor de voltaje de la batería + prueba de autonomía con la LiPo
- [ ] 8. Carcasa o correa

## Escuela: director y maestros
- [x] 8a. **Grupos** (3°A…): nombre, salón (zona) y maestro; asignar alumnos a su grupo
- [x] 8b. **Cuentas de personal**: invitación para maestros (como la de padres), alta y baja desde la página
- [x] 8c. **Mi grupo** (maestro): sus alumnos con zona, pulsera puesta/quitada y batería, resumen y plano solo con su grupo (filtrado en la API)
- [x] 8d. **Panel de la escuela** (director): plano con todos los niños como puntos, conteo por zona, filtro por grupo y buscador

## Seguridad y alertas
- [ ] 9. Alertas: SOS, pulsera quitada, sin señal y salida sin recogida (la escuela las atiende, el padre las ve)
- [ ] 10. Clave por nodo (que nadie mande lecturas falsas)

## Terminales
- [ ] 11. Terminal de tienda: NFC, catálogo, alérgenos y registro de consumo
- [ ] 12. Terminal de recogida: tarjeta + PIN + tap de la pulsera (llave) y cruce con la salida
- [ ] 13. Comprar lo pendiente: buzzer, 2N2222 y diodos

## App (PWA)
- [ ] 14. Pase de lista automático para el maestro
- [ ] 15. PWA instalable + notificaciones push
- [ ] 16. En "Mis hijos": consumo en la tienda e historial de recogidas

## Nube
- [x] 17. Desplegar en la nube (API + base de datos) y nodos con HTTPS — MonsterASP.NET: https://safeband-sibaja.runasp.net (falta activar el redirect a HTTPS y cambiar la contraseña de WebDeploy)

## Pruebas y entrega
- [ ] 18. Pruebas: 10 envíos seguidos, latencia, dato incorrecto y corte de WiFi
- [ ] 19. README del repositorio, video de demo y cronograma
- [ ] 20. Documentos: RF-22 en requisitos, Telegram en el Taller 2, Análisis de alternativas (Node/Python → ASP.NET/Azure) y bitácora del Taller 3

---

## Ya terminado (referencia)
- [x] API, base de datos, usuarios y roles, invitaciones para padres
- [x] Pantallas: En vivo, Alumnos, Pulseras, Tutores, Zonas y nodos, Mis hijos
- [x] Plano por zonas (paso 1) y plano del edificio 6J cargado (35 zonas)
- [x] Firmware de la pulsera en la XIAO ESP32-C3 y antena conectada
- [x] Flujo completo pulsera → nodo → API → panel con 2 nodos (Salón 8 y Proyectos)
- [x] Documentos: Requisitos, Modelos del comportamiento y Arquitectura
