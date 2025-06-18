#!/usr/bin/env python3
"""
Script to reconstruct a project from a text file dump.

Usage:
    python reconstruct_project.py <path_to_project_dump_file>

The project dump file should follow the format:
<./relative/path/to/file1>
<file1 content line 1>
<file1 content line 2>
...
<./relative/path/to/file2>
<file2 content line 1>
...

Or for skipped/binary files:
&&& FILE: ./relative/path/to/binary_or_error_file
&&& ERROR: Some error message why it was not included
"""

import os
import sys
import re

# Regex to identify file path lines like <./path/to/file.ext>
# It captures the path inside the angle brackets.
FILE_PATH_REGEX = re.compile(r"^<(\./.+?)>$")

# Regex for the binary/error file marker
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

    current_processing_file_path = None
    current_file_content_lines = []
    # Flag to indicate we are accumulating lines for a valid text file to be written
    is_writing_active_text_file = False

    line_iterator = iter(enumerate(lines))

    for line_number, line_text in line_iterator:
        stripped_line = line_text.strip()

        file_path_match = FILE_PATH_REGEX.match(stripped_line)
        binary_marker_match = BINARY_ERROR_FILE_MARKER_REGEX.match(stripped_line)

        if file_path_match:
            # Found a new text file marker <./path/to/file>
            # If there was a previous file being processed, write it
            if is_writing_active_text_file and current_processing_file_path and current_file_content_lines:
                write_content_to_file(current_processing_file_path, "".join(current_file_content_lines))

            # Start new file processing
            current_processing_file_path = file_path_match.group(1)  # Get the path like ./path/to/file
            current_file_content_lines = []
            is_writing_active_text_file = True
            # Content for this file starts from the next line, so the loop will handle it.

        elif binary_marker_match:
            # Found a binary/error file marker &&& FILE:
            # If there was a previous text file being processed, write it
            if is_writing_active_text_file and current_processing_file_path and current_file_content_lines:
                clean_content = "".join(current_file_content_lines).replace("\\_", "_")
                write_content_to_file(current_processing_file_path, clean_content)

            # This is a binary/error marker, so we stop processing the current_file_path as a writable text file
            binary_file_path_info = binary_marker_match.group(1)
            print(f"Info: Encountered non-text file definition: {binary_file_path_info}")

            # Check if the next line is the &&& ERROR: message
            try:
                _, next_line_text = next(line_iterator) # Consume the next line
                error_message_match = BINARY_ERROR_MESSAGE_REGEX.match(next_line_text.strip())
                if error_message_match:
                    print(f"  Reason: {error_message_match.group(1)}")
                else:
                    # If the next line wasn't an error message, it's unexpected,
                    # but we've already consumed it.
                    print(f"  Expected &&& ERROR: line, but found: {next_line_text.strip()}")
            except StopIteration:
                print(f"  Warning: &&& FILE: marker found at end of file without subsequent &&& ERROR: line.")


            current_processing_file_path = None  # Reset path
            current_file_content_lines = []
            is_writing_active_text_file = False  # Not writing this one

        elif is_writing_active_text_file and current_processing_file_path:
            # If we are in the process of accumulating content for an active text file
            current_file_content_lines.append(line_text) # Append the original line with its newline

    # After the loop, write the last processed file if any content was collected
    if is_writing_active_text_file and current_processing_file_path and current_file_content_lines:
        clean_content = "".join(current_file_content_lines).replace("\\_", "_")
        write_content_to_file(current_processing_file_path, clean_content)

    print("\nProject reconstruction attempt complete.")


def write_content_to_file(relative_path, content_string):
    """
    Writes the given content_string to the specified relative_path.
    The base directory is the script's current working directory.
    """
    base_dir = os.getcwd()  # Assumes script is run from the project root

    # Normalize path: remove leading './' or '.\'
    if relative_path.startswith(('./', '.\\')):
        relative_path = relative_path[2:]

    # Ensure path uses OS-specific separators by splitting and rejoining
    # This handles mixed separators like "backend\app/main.py"
    normalized_path_parts = relative_path.replace('\\', '/').split('/')
    full_path = os.path.join(base_dir, *normalized_path_parts)

    try:
        # Ensure the directory for the file exists
        file_directory = os.path.dirname(full_path)
        if file_directory: # Only create if dirname is not empty (i.e., not a root file)
            os.makedirs(file_directory, exist_ok=True)

        with open(full_path, 'w', encoding='utf-8') as f:
            f.write(content_string)
        print(f"Updated/Created: {relative_path} (at {full_path})")
    except OSError as e:
        print(f"Error writing file {full_path}: {e}")
    except Exception as e:
        print(f"An unexpected error occurred while writing {full_path}: {e}")


def main():
    input_file = ".gpt/response.txt"
    reconstruct_project_from_dump(input_file)


if __name__ == "__main__":
    main()