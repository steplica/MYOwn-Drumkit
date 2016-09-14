# MYOwn Drumkit

This project was hacked together in 24 hours with 1 teammate during RIT's hackathon, BrickHack 2.

MYOwn Drumkit uses two Myo Armbands to simulate an air drumkit. You simply hold one in each hand, set a point of origin, and begin drumming. It reads in streams of data for orientation, gyroscope information, and accelerometer data.

These streams of data are used to determine where your hands are, relative to the origin point, and how fast they are moving. It's able to tell where the snare, hi-hat, crash cymbal, ride cymbal, and bass drum are and when you've hit them in order to produce the respective sound.
