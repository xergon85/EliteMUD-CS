#!/usr/bin/env python3
"""
EliteMUD Equipment Persistence Test
Tests that equipped items persist across logout/login cycles
"""

import socket
import time
import sys

SERVER = "localhost"
PORT = 7500
ACCOUNT = "testpersist"
PASSWORD = "test123"
CHAR_NAME = "TestChar"


def send_and_receive(sock, message="", delay=0.5):
    """Send a message and receive response"""
    if message:
        sock.sendall((message + "\r\n").encode("utf-8"))
    time.sleep(delay)

    try:
        data = sock.recv(4096).decode("utf-8", errors="ignore")
        return data
    except:
        return ""


def test_session_1():
    """Session 1: Create character, equip item, save"""
    print("\n=== SESSION 1: Create Character and Equip Item ===\n")

    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.connect((SERVER, PORT))

    # Initial greeting
    response = send_and_receive(sock)
    print(response)

    # Account name
    response = send_and_receive(sock, ACCOUNT)
    print(response)

    # Confirm account name
    if "Did I get that right" in response or "right" in response.lower():
        response = send_and_receive(sock, "Y")
        print(response)

    # New account - set password
    if "password" in response.lower():
        response = send_and_receive(sock, PASSWORD)
        print(response)

        # Confirm password
        response = send_and_receive(sock, PASSWORD)
        print(response)

    # Create new character (type 'n')
    response = send_and_receive(sock, "n", 1.0)
    print(response)

    # Character name
    response = send_and_receive(sock, CHAR_NAME)
    print(response)

    # Confirm character name
    if "right" in response.lower():
        response = send_and_receive(sock, "Y")
        print(response)

    # Select sex (m = Male) - comes first in flow
    response = send_and_receive(sock, "m", 1.0)
    print(response)

    # Select race (a = Human)
    response = send_and_receive(sock, "a", 1.0)
    print(response)

    # Select class (d = Warrior)
    response = send_and_receive(sock, "d", 1.5)
    print(response)

    # Now in game - look around
    response = send_and_receive(sock, "look", 1.0)
    print(response)

    # Get the practice sword
    print("\n--- Getting and wielding sword ---")
    response = send_and_receive(sock, "get sword", 1.0)
    print(response)

    # Wield the sword
    response = send_and_receive(sock, "wield sword", 1.0)
    print(response)

    # Check equipment
    print("\n--- Checking equipment ---")
    response = send_and_receive(sock, "equipment", 1.0)
    print(response)

    # Manual save
    print("\n--- Manual save ---")
    response = send_and_receive(sock, "save", 1.0)
    print(response)

    # Quit
    print("\n--- Quitting ---")
    response = send_and_receive(sock, "quit", 1.0)
    print(response)

    sock.close()

    return True


def test_session_2():
    """Session 2: Login and verify equipment persisted"""
    print("\n\n=== SESSION 2: Login and Verify Equipment ===\n")

    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.connect((SERVER, PORT))

    # Initial greeting
    response = send_and_receive(sock)
    print(response)

    # Account name
    response = send_and_receive(sock, ACCOUNT)
    print(response)

    # Confirm account name
    if "Did I get that right" in response or "right" in response.lower():
        response = send_and_receive(sock, "Y")
        print(response)

    # Password
    response = send_and_receive(sock, PASSWORD)
    print(response)

    # Select character (option 1)
    response = send_and_receive(sock, "1", 1.5)
    print(response)

    # Check equipment - this is the critical test!
    print("\n--- Checking equipment (should show sword!) ---")
    response = send_and_receive(sock, "equipment", 1.0)
    print(response)

    # Check inventory
    print("\n--- Checking inventory ---")
    response = send_and_receive(sock, "inventory", 1.0)
    print(response)

    # Check score
    print("\n--- Checking score ---")
    response = send_and_receive(sock, "score", 1.0)
    print(response)

    # Quit
    print("\n--- Quitting ---")
    response = send_and_receive(sock, "quit", 1.0)
    print(response)

    sock.close()

    # Analyze results
    if "practice sword" in response.lower() or "sword" in response.lower():
        return True
    return False


if __name__ == "__main__":
    try:
        print("=" * 60)
        print("EliteMUD Equipment Persistence Test")
        print("=" * 60)

        # Session 1: Create and equip
        success1 = test_session_1()

        # Wait between sessions
        print("\n\n>>> Waiting 3 seconds between sessions...\n")
        time.sleep(3)

        # Session 2: Verify persistence
        success2 = test_session_2()

        # Results
        print("\n" + "=" * 60)
        print("TEST RESULTS")
        print("=" * 60)
        if success2:
            print("✅ SUCCESS: Equipment persisted across logout/login!")
        else:
            print("❌ FAILURE: Equipment did not persist")
        print("=" * 60)

    except Exception as e:
        print(f"\n❌ Error: {e}")
        sys.exit(1)
