# Just Another Cooking Skill

Adds cooking skill into the game which increases when player is using Cooking Station and Cauldron;

Adds quality variants for food cooked: Awful, Poor, Normal, Well, Delicious.

Based on random value adjusted by cooking skill level, there is a chance to cook different quality of meals. By default, quality changes amount of health/stamina gained by food for 15% per quality(configurable). For exaple, Delicious Cooked Meat has 30% more health/stamina than Normal(default item).

Also, 'well' and 'delicious' food increases regenation gained by 1/2 hp/sec.

Installation
Plugin is dependant on dev build of JotunnLib (https://github.com/jotunnlib/jotunnlib), thats why JotunnLib.dll is also included. I will omit this dependency once there will be new release of JotunnLib.
Download via Vortex or extract .dll file and assets folder to BepInEx/plugins 

Credits:

Thanks to 
https://github.com/jotunnlib/jotunnlib - modding library used
https://github.com/thegreyham/Valheim.CookingSkill - original idea from thegreyham, also I've used some harmony patches from his repository 

