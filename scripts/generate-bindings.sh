
#!/bin/bash
cd bindgen
python3 gen.py
rm -rf *.json
rm -rf __pycache__
cd ..
