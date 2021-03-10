import os

def convert(in_file, skip_frames_start, skip_frames_end, source_fps=120, target_fps=10):
    frame_time = 1.0 / target_fps
    out_file = in_file.split(".bvh")[0] + "_reduced.txt"

    with open(in_file, "r") as f:
        text = f.readlines()

    new_text = []
    for line_idx, line in enumerate(text):
        new_text.append(line)
        if "MOTION\n" in line:
            text = text[line_idx+1:]
            break

    frames = int(text[0].split("Frames: ")[1])
    new_frames = (frames - skip_frames_start - skip_frames_end) // (source_fps // target_fps)
    new_text.append(f"Frames: {new_frames}\n")
    new_text.append(f"Frame Time: {frame_time:0.7f}\n")

    text = text[2:]

    for i in range(skip_frames_start, frames - skip_frames_end):
        if i % (source_fps // target_fps) == 0:
            line = text[i]
            new_text.append(line)
    new_text.append("\n")


    with open(out_file, "w+") as f:
        f.writelines(new_text)


convert("TABLE_stand_grabCup_lookAtCup_drinkCup_putDownCup.bvh", skip_frames_start=1080, skip_frames_end=600)
convert("NO-TABLE_idle_sit_jump_crouch_walk_run_dance.bvh", skip_frames_start=1800, skip_frames_end=600)
# convert("rom.bvh", skip_frames_start=2760, skip_frames_end=12747)
convert("TABLE_stand_2.bvh", skip_frames_start=600, skip_frames_end=600)
convert("NO-TABLE_sit_1.bvh", skip_frames_start=600, skip_frames_end=600)
convert("talking-gestures.bvh", skip_frames_start=9350, skip_frames_end=13362)