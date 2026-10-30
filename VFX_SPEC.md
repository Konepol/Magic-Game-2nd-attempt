# Spell VFX and cast animation spec (from the user)

Work is done spell by spell (visuals and cast animation together). Includes screen-wide effects (Absolute Zero dim, camera shake, ground markers, ground cracks).
Status is tracked at the bottom.

## What each spell should look like (flight, trail, impact)

### Base spells
- **Arcane Bolt**: purple-blue glowing orb with a short star trail. Impact: small violet pop. Hold: large, slow, bright orb with a wider trail.
- **Scatter Shot**: fan of 5 small violet shards spreading at launch. Each hit a tiny spark. Hold: denser, wider fan of 7.
- **Spark Stream**: rapid stream of tiny, thin, bright yellow-white sparks like tracers. Almost no impact, only a small flicker.
- **Fire Burst**: orange-red fireball with an ember trail. Ends in a round fire explosion at radius 4: flash, expanding ring, rising embers. Hold: bigger, darker fireball, larger blast.
- **Frost Shard**: angular ice crystal, pale cyan-white, glittering snow trail. Hit: icy shatter and a few flying shards. Hold: larger, longer crystal.
- **Wind Blade**: thin crescent of pale green-white air, spinning or slicing, streaky motion lines. Hit: quick slash and gust puff.
- **Venom Dart**: small sharp dart, toxic green-purple, drips a trail. Hit: green splash and lingering poison mist.
- **Lightning Lance**: jagged yellow-blue bolt, crackles and flickers. Fires instantly, forks slightly. Impact: bright flash with electric arcs.
- **Radiant Beam**: straight steady beam of golden-white holy light, thicker and smoother than lightning, soft glow, bright starburst where it lands.
- **Earth Spike**: stone spikes erupting from the ground at the target, brown and grey. Dirt and rock debris fly up, dust, small ground shake.
- **Meteor Strike**: large flaming rock falling from the sky with a long fire trail and a ground marker beforehand. Lands with a huge crater blast, shockwave ring, debris and smoke. Camera shake.
- **Arcane Ward**: translucent purple-blue barrier wall with a hex or rune pattern. Ripples when hit, fades out when it expires.

### Fusions (two source elements merged, visibly more intense than either parent)
- **Infernal Bolt**: white-hot core inside a swirling red-black fire sheath. Explodes with a big fireball and a dark smoke ring.
- **Cinder Storm**: cluster of small glowing embers bursting into many mini-explosions. A scattered ember shower, not one big blast.
- **Firestorm Lance**: long spear of fire with a spinning flame spiral around it. Ends in a wide, fierce blast.
- **Glacier Bolt**: big chunk of blue ice that shatters into a wide frost burst. Freezing mist spreads, ice spikes poke up.
- **Cyclone**: spinning green-white funnel of wind with debris. On impact a swirling tornado ring that pulls things inward.
- **Plague Storm**: swirling toxic cloud with green-purple spores. Explodes into a lingering poison cloud with bubbling particles.
- **Blizzard Lance**: icy white-blue beam with snow and ice crystals swirling around it. Leaves a frosty burst where it ends.
- **Sun Lance**: thick blinding gold-white beam like a mini sun, lens flare and heat shimmer. Scorches the ground at the end point. Most powerful beam.
- **Thunder Meteor**: meteor wrapped in lightning striking down with a crackling flash. Impact: crater with electric arcs and a bright white flash.
- **Tremor Storm**: repeated ground eruptions across a wide area. Cracks spread, spikes burst up in waves, heavy dust and screen shake.
- **Absolute Zero**: screen dims, then a white-blue freeze wave sweeps outward. Ice crystals grow across the ground, huge frozen burst follows. Anything hit looks frozen solid.

## Casting animations

### Base spells
- **Arcane Bolt**: thrusts a hand forward. Violet orb gathers at the palm, then launches. Tap: quick flick. Hold: slower two-hand charge with the orb swelling.
- **Scatter Shot**: hand sweeps sideways in an arc. Small violet sparks gather and spray out in a fan. Hold: wider sweep, bigger spray.
- **Spark Stream**: arm stays extended, hand shakes slightly. Small spark flickers at the fingertips, shots keep coming. No big wind-up.
- **Fire Burst**: fast overhead or side throw. Embers swirl around the hand, flame flare pops at release. Hold: longer wind-up, flames growing, heat shimmer.
- **Frost Shard**: hand pulls back and snaps forward. Ice crystals form at the fingertips, shard fires with a cold mist puff.
- **Wind Blade**: horizontal slash with the arm. Green-white arc streaks across the body, blade leaves the end of the swing.
- **Venom Dart**: quick sidearm flick. Dart forms between the fingers in a green puff of drips, then is thrown.
- **Lightning Lance**: arm points straight ahead. Electricity crackles up the arm into the palm, sharp discharge fires with a small recoil.
- **Radiant Beam**: both hands raised and brought together. Golden light grows between them, palms push forward, beam streams out. Caster holds the pose while active.
- **Earth Spike**: stomp or fist slam down. Dust rings out from the ground, then spikes erupt at the target.
- **Meteor Strike**: arms raised overhead, hands pointing at the sky. Red glow gathers above, both arms swing down to call the meteor.
- **Arcane Ward**: palm held out flat like a stop sign, rune circle appears in front of it. Barrier unfolds outward from the palm.

### Fusions (two-element cast: both parent effects spiral together in the hands before release)
- **Infernal Bolt**: both hands cup a fireball turning white-hot and red-black. Raised and hurled forward with a heavy follow-through.
- **Cinder Storm**: hands open, embers swarm up around the caster. Forward shove sends the cluster out.
- **Firestorm Lance**: flame spear in one hand with spiral fire along it. Arm draws back, spear thrown.
- **Glacier Bolt**: hands pull in cold mist that hardens into an ice chunk. Heavy two-hand shove launches it.
- **Cyclone**: caster spins in place or spins the hands in a circle. Funnel builds in the palm, then released.
- **Plague Storm**: hands cup a swirling green cloud that boils and grows. Forward push sends it out.
- **Blizzard Lance**: arms held out, snow swirls up the arm. Beam fires from the palm with cold vapour. Pose held while firing.
- **Sun Lance**: long two-hand charge, glowing sun orb forming above the staff or hands, screen brightens slightly. Both arms thrust forward, beam fires with a flare.
- **Thunder Meteor**: hand raised as the sky darkens and sparks crackle. Downward swing calls the strike.
- **Tremor Storm**: stomp with both fists grinding into the ground. Ground rumbles and cracks run outward before the eruptions.
- **Absolute Zero**: caster rises slightly, both arms spread wide, air frosts and light dims. Clap or slam unleashes the freeze wave.

## Status
Done: Arcane Bolt (violet colours, small violet pop impact, tap thrust and two-handed hold charge with a swelling hand orb; cast styles live in SpellCastPose.cs, impacts in SpellImpact.cs).
Next: Scatter Shot, then down the list in the order above. Cast styles are added as entries in CastStyles.Build; a spell without one keeps the shared wand animation.
