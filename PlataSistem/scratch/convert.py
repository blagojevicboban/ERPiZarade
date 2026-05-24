import os

def main():
    src_path = r"c:\PLATA\OBRAC.PRG"
    dest_path = r"c:\PLATA\PlataSistem\scratch\obrac_utf8.prg"
    
    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    
    # Try reading with cp852
    try:
        with open(src_path, "r", encoding="cp852") as f:
            content = f.read()
    except Exception as e:
        print(f"Failed with cp852: {e}, trying cp1250")
        try:
            with open(src_path, "r", encoding="cp1250") as f:
                content = f.read()
        except Exception as e2:
            print(f"Failed with cp1250: {e2}, trying latin1")
            with open(src_path, "r", encoding="latin1") as f:
                content = f.read()
                
    with open(dest_path, "w", encoding="utf-8") as f:
        f.write(content)
        
    print("Successfully converted OBRAC.PRG to UTF-8")

if __name__ == "__main__":
    main()
