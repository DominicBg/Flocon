# Drawing phase
Just draw a snowflake, then press "B"

Once you draw 3, sit back and enjoy the small visual


# Tech stuff
- Drawing is represented with Line Renderers.
- Each line is duplicated/mirrored to look like a snowflake
- Once drawn, its turned into a 3D Mesh with a desired thickness
- A Particle Systems will use the 3 snowflakes mesh
- 3 Flakes will be Game Objects falling with a certain noise
- A spline is created to intersect the center of the flake at a the right time

