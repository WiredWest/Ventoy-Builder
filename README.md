<img width="1823" height="863" alt="image" src="https://github.com/user-attachments/assets/23667c55-ed8a-41e7-b19c-e942ef287f06" />
# Ventoy Builder

**Ventoy Builder** is a user-friendly Windows frontend for building, organizing, and customizing Ventoy-based multiboot USB drives.

Ventoy is powerful, but it can feel intimidating for users who are not comfortable with command-line tools, folder structures, plugin files, or boot menu configuration. Ventoy Builder wraps those tasks in a guided desktop interface that helps users select a USB drive, install or update Ventoy, organize boot images, customize menu names, reorder boot entries, and generate a custom Ventoy menu file.

## Features

* Guided step-by-step builder mode for beginners
* Select and inspect removable USB drives
* Install or update Ventoy from inside the app
* Launch the official Ventoy tool for advanced options
* Create recommended folder layouts automatically
* Scan folders for supported boot images
* Supports ISO, IMG, WIM, VHD, and VHDX files
* Boot image library with detected type, architecture, and size
* Copy boot images to organized USB folders
* View current USB contents
* Open or delete boot images directly from the USB
* Drag-and-drop boot menu ordering
* Custom friendly boot menu names
* Automatic `ventoy.json` generation
* Automatic backup of existing Ventoy menu files
* Live activity log
* Circular progress gauge with real-time status
* Resizable modern dark UI

## Why This Exists

Ventoy is excellent for creating multiboot USB drives, but many users still need help with questions like:

* Which USB drive am I working on?
* Where should ISO files go?
* How do I organize Windows, Linux, recovery, and utility images?
* How do I make the boot menu easier to read?
* How do I safely generate a Ventoy configuration file?
* What is the next step?

Ventoy Builder is designed to make those steps clearer, safer, and easier.

## Typical Workflow

1. Select the USB drive.
2. Install or update Ventoy.
3. Create the recommended folder layout.
4. Scan your computer for boot images.
5. Copy boot images to the USB.
6. Reorder the boot menu.
7. Rename entries with friendly display names.
8. Generate the custom Ventoy menu.

## Intended Users

Ventoy Builder is useful for:

* Computer repair technicians
* IT support workers
* MSPs and consultants
* Hobbyists
* Schools and small offices
* Anyone who wants a clean, organized multiboot USB without manually editing JSON files

## Project Status

This project is currently under active development. Core functionality is working, including USB selection, ISO library scanning, copying boot images, custom menu ordering, alias generation, and Ventoy menu creation.

Future improvements may include:

* Ventoy theme customization
* Saved build profiles
* ISO checksum verification
* Duplicate detection
* Better OS/version detection
* One-click toolkit presets
* Portable release packaging

## Disclaimer

Ventoy Builder is an independent frontend utility and is not affiliated with or endorsed by the official Ventoy project. Ventoy itself is created and maintained by the Ventoy developers.

Always double-check the selected USB drive before installing or updating Ventoy.
