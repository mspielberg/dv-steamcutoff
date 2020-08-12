# Realistic Steam Cutoff

## Background
### What is cutoff?

Cutoff is the point at which the intake valve closes, preventing more steam from traveling from boiler into the cylinder. Before the cutoff point, the steam in the cylinder is nearly full boiler pressure. After the cutoff point, no further steam enters the cylinder, and the pressure drops as the piston moves. However, the steam already in the cylinder continues to push against the piston, giving "free" power. This is called "expansive working," and is key to efficient operation of a steam engine.

If we lengthen the cutoff, the valve closes later, more steam travels from the boiler to the cylinder, and cylinder pressure remains high for a longer time.

![Graph of cylinder presssure vs. piston position at 20% cutoff](https://raw.githubusercontent.com/mspielberg/dv-steamcutoff/master/resources/pressure-c20.png)
![Graph of cylinder presssure vs. piston position at 80% cutoff](https://raw.githubusercontent.com/mspielberg/dv-steamcutoff/master/resources/pressure-c80.png)

[(Click here for an animated version of these graphs)](https://www.desmos.com/calculator/3sw1msnaxm)

The work done by the steam on the piston is the area under the curve. Let's see how the work done, and consequently the power output of the steam engine, changes with the length of the cutoff:

![Graph of power vs. cutoff setting with highlight at 20% cutoff](https://raw.githubusercontent.com/mspielberg/dv-steamcutoff/master/resources/power-c20.png)
![Graph of power vs. cutoff setting with highlight at 80% cutoff](https://raw.githubusercontent.com/mspielberg/dv-steamcutoff/master/resources/power-c80.png)

The lower line shows how much steam is admitted to the cylinder, and the upper shows the total work done by that steam. The distance between the two lines is the amount of "free" energy obtained through expansive working. What if we normalize this graph by the amount of steam consumed? In other words, what is the effect of cutoff on efficiency?

![Graph of efficiency vs. cutoff setting](https://raw.githubusercontent.com/mspielberg/dv-steamcutoff/master/resources/efficiency-c20.png)

At a cutoff setting of 20%, we get 2.6 times as much power from the same amount of steam as we would at 100% cutoff, and lower cutoff settings are even better!

### What about the throttle?

The effect of the throttle is to limit the pressure sent to the cylinders. Since the graphs are all normalized to 100% being the input pressure, changing the throttle scales the pressure and power graphs vertically, but leaves the efficiency unchanged since that is already normalized by steam consumption.

[(Click here for an animated graph of the effect of the throttle)](https://www.desmos.com/calculator/pqa2dvddzs)

## About This Mod

In vanilla Derail Valley, the SH282 behaves oddly and unrealistically: There is a different cutoff wheel position for optimum power that depends on the locomotive's speed, but the cutoff wheel has no effect on engine efficiency. Steam consumption is influenced only by torque output, even if the locomotive is not moving, and no steam is theoretically being admitted into or ejected from the cylinders.

Realistic Steam Cutoff makes several changes to the behavior of the SH282 steam locomotive in Derail Valley.

1. Realistic cutoff setting modelling, inline with the theory described above. Longer cutoff (further from center) always gives more power, and shorter cutoff (closer to the center) gives better efficiency.
2. Steam consumption is proportional to locomotive speed. More speed = more piston strokes = more steam required. 
3. Boiler steam production and maximum pressure is substantially reduced.

In combination, this has some very noticeable effects on how the steamer performs:

* You will spend most of your time underway at very low cutoff settings, 15% or less, just as in reality.

* As long as you have the traction necessary to move the train, you can climb grades of virtually any length without running out of steam. The SH282 is capable of moving massive loads up grades, albeit slowly. Managing your sand is vital, and be careful not to lose steam through the safety valve!

* The boiler has significant trouble maintaining pressure when sustaining high-speeds of 80+ kph. Just as in reality, this is the most demanding situation for a boiler's power output. Historically, the desire for higher train speeds is what drove railroads to seek larger fireboxes and greater steam generating capacity.

* It is quite viable to use the SH282 as an efficient shunter, or even as a quasi-fireless locomotive, operating at low speeds and high tractive effort from steam pressure in the boiler.
