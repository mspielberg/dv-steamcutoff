# Per-locomotive constants

## Smokebox
Chimney/stack passive draft rate (kg/s)
Exhaust blast efficiency (kg draft per kg exhaust steam)
Blower max flow rate (kg/s)

## Firebox
Firebox capacity (kg)
Stoker max coal feed rate (kg/s) (*)

## Boiler
Boiler total volume (L)
Max boiler pressure (bar, safety valve opening threshold)
Feedwater temperature (C)

## Engine
Cylinder total volume (L)
Minimum/Maximum cutoff (% of cylinder volume)
Driver size (m, radius/diameter/circumference)

# Per-tick simulation inputs

## Smokebox
Blower rate (%)

## Firebox
Damper opening (%)
Firebox fill (kg)

## Boiler
Injection rate (kg)
Regulator opening (%)
Steam loss rate (kg, from damage, dump valve, etc.)

## Cylinder
Cutoff setting (%)
Locomotive speed (km/h, controls transition to time-averaged torque simulation)

# Per-tick simulation outputs
Fire temperature (C)
Torque output to rails (%)
Combustion quality (for smoke color)

# Intentionally ignored as insignificant
Injector steam consumption rate
Stoker steam consumption rate

# Significant, but maybe too fiddly to simulate
Fuel composition (heat energy / kg)
Fuel surface area ratio (i.e. size of each piece, affects burn rate)
Cylinder insulation

# Not simulated, but probably interesting
Cylinder arrangement (count, compounding layout)