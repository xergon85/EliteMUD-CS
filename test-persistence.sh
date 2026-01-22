#!/bin/bash
# EliteMUD Persistence Test Script

SERVER="localhost"
PORT="7500"
ACCOUNT="testuser"
PASSWORD="test123"

echo "=== EliteMUD Persistence Test ==="
echo ""
echo "This script will test equipment persistence by:"
echo "1. Creating a test account and character"
echo "2. Equipping items"
echo "3. Saving manually"
echo "4. Logging out and back in"
echo "5. Verifying items are still equipped"
echo ""
echo "Server: $SERVER:$PORT"
echo "Account: $ACCOUNT"
echo ""
echo "Press Ctrl+C to cancel, or Enter to continue..."
read

# Session 1: Create character and equip items
echo ""
echo "=== Session 1: Creating character and equipping items ==="
{
  sleep 2
  echo "$ACCOUNT"
  sleep 1
  echo "Y"
  sleep 1
  echo "$PASSWORD"
  sleep 1
  echo "$PASSWORD"
  sleep 2
  echo "1"  # Create new character
  sleep 1
  echo "TestChar"
  sleep 1
  echo "Y"
  sleep 1
  echo "1"  # Select race (Human)
  sleep 1
  echo "1"  # Select class (Warrior)
  sleep 1
  echo "1"  # Select sex (Male)
  sleep 2
  echo "look"
  sleep 1
  echo "get sword"
  sleep 1
  echo "wield sword"
  sleep 1
  echo "equipment"
  sleep 2
  echo "save"
  sleep 2
  echo "quit"
  sleep 1
} | telnet $SERVER $PORT 2>&1 | tee session1.log

echo ""
echo "=== Waiting 3 seconds before Session 2 ==="
sleep 3

# Session 2: Login and verify equipment
echo ""
echo "=== Session 2: Logging back in to verify persistence ==="
{
  sleep 2
  echo "$ACCOUNT"
  sleep 1
  echo "Y"
  sleep 1
  echo "$PASSWORD"
  sleep 2
  echo "1"  # Select TestChar
  sleep 2
  echo "equipment"
  sleep 2
  echo "inventory"
  sleep 2
  echo "score"
  sleep 2
  echo "quit"
  sleep 1
} | telnet $SERVER $PORT 2>&1 | tee session2.log

echo ""
echo "=== Test Complete ==="
echo ""
echo "Check the logs:"
echo "  session1.log - Character creation and equipment"
echo "  session2.log - Login verification"
echo ""
echo "Look for 'a practice sword' in the equipment output of session2.log"
echo "If it appears, persistence is working correctly!"
