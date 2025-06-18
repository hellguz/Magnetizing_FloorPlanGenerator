#!/usr/bin/env python3
"""
Script to reconstruct a project from a text file dump.

This revised script corrects the regular expression to handle file markers
that may or may not start with a '/'. This fixes the file concatenation bug.
"""

import os
import sys
import re

# Regex to identify file path lines like <./path/to/file.ext>
# The '[/]?' part makes a leading slash inside the brackets optional.
FILE_PATH_REGEX = re.compile(r"^<[/]?(\./.+?)>$")

# Regex for markers of files that were skipped (e.g., binary files)
BINARY_ERROR_FILE_MARKER_REGEX = re.compile(r"^\&\&\& FILE: (.+?)$")
BINARY_ERROR_MESSAGE_REGEX = re.compile(r"^\&\&\& ERROR: (.+?)$")


def reconstruct_project_from_dump(input_dump_file_path):
    """
    Parses the input dump file and reconstructs the project structure and files.
    """
    if not os.path.isfile(input_dump_file_path):
        print(f"Error: Input dump file '{input_dump_file_path}' not found.")
        sys.exit(1)

    with open(input_dump_file_path, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()

    current_file_path = None
    current_file_lines = []
    is_writing_active = False

    line_iterator = iter(enumerate(lines))

    for line_number, line_text in line_iterator:
        stripped_line = line_text.strip()
        path_match = FILE_PATH_REGEX.match(stripped_line)
        binary_marker_match = BINARY_ERROR_FILE_MARKER_REGEX.match(stripped_line)

        is_new_file_marker = path_match or binary_marker_match

        # If we encounter any new file marker, the previous file (if any) is complete and should be written.
        if is_new_file_marker and is_writing_active and current_file_path:
            write_content_to_file(current_file_path, current_file_lines)
            # Reset for the next file
            current_file_path = None
            current_file_lines = []
            is_writing_active = False

        # Now, handle the new marker
        if path_match:
            current_file_path = path_match.group(1)
            is_writing_active = True
            # Content starts on the next line
            
        elif binary_marker_match:
            binary_file_path_info = binary_marker_match.group(1)
            print(f"Info: Skipped file definition found: {binary_file_path_info}")
            try:
                # Consume the next line to check for an error message
                _, next_line_text = next(line_iterator)
                error_match = BINARY_ERROR_MESSAGE_REGEX.match(next_line_text.strip())
                if error_match:
                    print(f"  Reason: {error_match.group(1)}")
                else:
                    print(f"  Warning: Expected '&&& ERROR:' line, but found: {next_line_text.strip()}")
            except StopIteration:
                print("  Warning: '&&& FILE:' marker at end of file.")
        
        # If we are not on a marker and are actively collecting lines for a file, append the line
        elif is_writing_active and current_file_path:
            current_file_lines.append(line_text)

    # After the loop, write the last file if it exists
    if is_writing_active and current_file_path and current_file_lines:
        write_content_to_file(current_file_path, current_file_lines)

    print("\nProject reconstruction complete.")


def write_content_to_file(relative_path, content_lines):
    """
    Writes the given list of content lines to the specified relative_path.
    """
    content_string = "".join(content_lines)
    
    base_dir = os.getcwd()

    if relative_path.startswith(('./', '.\\')):
        relative_path = relative_path[2:]

    normalized_path_parts = relative_path.replace('\\', '/').split('/')
    full_path = os.path.join(base_dir, *normalized_path_parts)

    try:
        file_directory = os.path.dirname(full_path)
        if file_directory:
            os.makedirs(file_directory, exist_ok=True)

        with open(full_path, 'w', encoding='utf-8') as f:
            f.write(content_string)
        print(f"Updated/Created: {relative_path}")
    except OSError as e:
        print(f"Error writing file {full_path}: {e}")
    except Exception as e:
        print(f"An unexpected error occurred while writing {full_path}: {e}")


def main():
    """
    Main function to run the script.
    """
    if len(sys.argv) > 1:
        input_file = sys.argv[1]
    else:
        input_file = ".gpt/response.txt"
        print(f"No input file provided. Using default: '{input_file}'")

    reconstruct_project_from_dump(input_file)


if __name__ == "__main__":
    main()