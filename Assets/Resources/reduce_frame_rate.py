import os

source_framerate = 120
target_framerate = 15
frame_time = 1.0 / target_framerate
skip_frames = 300
file = "TABLE_sit_grabCup_lookAtCup_drinkCup_putDownCup_merged.txt"
out_file = "TABLE_sit_grabCup_lookAtCup_drinkCup_putDownCup_merged_reduced.txt"

with open(file, "r") as f:
    text = f.readlines()

new_text = []
for line_idx, line in enumerate(text):
    new_text.append(line)
    if "MOTION\n" in line:
        text = text[line_idx+1:]
        break

frames = int(text[0].split("Frames: ")[1])
new_frames = (frames - skip_frames) // (source_framerate // target_framerate)
new_text.append(f"Frames: {new_frames}\n")
new_text.append(f"Frame Time: {frame_time:0.7f}\n")

text = text[2:]

for i in range(skip_frames, frames):
    if i % (source_framerate // target_framerate) == 0:
        line = text[i]
        new_text.append(line)
new_text.append("\n")


with open(out_file, "w+") as f:
    f.writelines(new_text)
