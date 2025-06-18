import os

# Initialize an empty list to store the paths of all objects
objects_list = []

# Define folders to exclude
exclude_folders = [
    'node_modules', 'venv', '.venv', '__pycache__', 'env', '.env', '.cache', '.mypy_cache', '.pytest_cache',
    'build', 'dist', 'bin', 'obj', '.git', '.github', '.gpt', '.idea', '.vscode',
    'coverage', '.coverage', '.tox', '.eggs', 'eggs', '.gradle', '.svn', '.DS_Store',
    'tileset', 'postgres_data', 'data', 'datasets', 'logs', 'doc', 'docs', 'tmp', 'temp', '.dump.log', '.vs', '.vscode',
    'out', 'output', 'results', 'reports', 'test_results', 'tests', 'testdata', 'examples', 'sample_data',
]

# Allowed file extensions
allowed_extensions = (
    '.txt', '.yaml', '.yml', '.jsonl', '.ini', '.sh', '.env', '.cfg', '.conf', '.config', '.log', '.srt', '.md',
    '.js', '.jsx', '.ts', '.tsx', '.mjs', '.cjs', '.vue', '.svelte', '.html', '.htm', '.xhtml', '.css', '.scss', '.sass', '.less',
    '.java', '.class', '.jsp', '.jspx', '.py', '.pyw', '.pyx', '.pxd', '.pxi', '.pyc', '.pyo', '.pyd', 'Dockerfile', '.sln', '.csproj', '.vbproj',
    '.cs', '.cpp', '.c', '.h', '.hpp', '.hxx', '.cxx', '.go', '.rs', '.swift', '.kt', '.kts', '.csproj.user', '.csproj.lock', '.gitignore', '.gitattributes',
)

def generate_tree(directory, prefix=''):
    """
    Recursively generate a tree structure of all files and directories.
    """
    tree = []
    try:
        entries = os.listdir(directory)
    except PermissionError:
        return ["[ACCESS DENIED]"]
    
    for idx, entry in enumerate(entries):
        full_path = os.path.join(directory, entry)
        connector = "├── " if idx < len(entries) - 1 else "└── "
        tree.append(f"{prefix}{connector}{entry}")
        if os.path.isdir(full_path) and not entry.startswith('.') and entry not in exclude_folders:
            subtree = generate_tree(full_path, prefix + ("│   " if idx < len(entries) - 1 else "    "))
            tree.extend(subtree)
    return tree

# Generate the tree structure for the current directory
# directory_tree = generate_tree("./compute.rhino3d/src")

# Write the tree structure and filtered list of objects to 'dump.txt'
with open('./.gpt/dump.txt', 'w', encoding='utf-8') as f:
    # Write the directory tree at the beginning
    # f.write("&&& DIRECTORY TREE &&&\n\n")
    # f.write("\n".join(directory_tree) + "\n\n")
    
    # Walk through the current directory and its subdirectories
    for dirpath, dirnames, filenames in os.walk(r"./"):
        # Skip directories starting with "." or in the exclude list
        dirnames[:] = [d for d in dirnames if not d.startswith('.') and d not in exclude_folders]

        # Add files with allowed extensions to the list
        for name in filenames:         
            objects_list.append(os.path.join(dirpath, name))

    # Write filtered objects and their content to the dump file
    for obj in objects_list:
        if os.path.isfile(obj):
            try:
                with open(obj, 'r', encoding='utf-8') as o:
                    if  obj.endswith(allowed_extensions):
                        content = o.read()
                    else:
                        content = o.read()[:20] + "\n................................"
                    f.write("<" + obj + '>\n' + content + '\n\n')
            except (IOError, UnicodeDecodeError) as e:
                f.write(f"&&& FILE: {obj}\n&&& ERROR: Could not read file: {e}\n\n")
