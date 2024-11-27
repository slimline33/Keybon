#include <USBComposite.h>
#include <SPI.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <Button.h> // https://github.com/JChristensen/Button
#include "bmp.h"

USBHID HID;
HIDKeyboard Keyboard(HID);
HIDConsumer Consumer(HID);
USBCompositeSerial CompositeSerial;

#define PRODUCT_ID 0x29

// Pin-Konfigurationen
const uint8_t switchPins[] = {PA3, PB0, PB12, PA15, PB11, PA9, PA14, PB10, PA8};
const uint8_t oledCSPins[] = {PB5, PA4, PB13, PB4, PA6, PB14, PB3, PB1, PB15};

#define OLED_MOSI PA7
#define OLED_CLK  PA5
#define OLED_DC   PA2
#define OLED_RESET PA1

// Arrays für Buttons und OLEDs
Button buttons[9];
Adafruit_SSD1306* oleds[9];

unsigned int cursor = 0;
boolean defaultLayoutActive = true;

void setup() {
  // Pins initialisieren
  for (uint8_t i = 0; i < 9; i++) {
    pinMode(switchPins[i], INPUT_PULLUP);
    buttons[i] = Button(switchPins[i], true, true, 5);
    oleds[i] = new Adafruit_SSD1306(128, 48, OLED_MOSI, OLED_CLK, OLED_DC, OLED_RESET, oledCSPins[i]);
    oleds[i]->begin(SSD1306_SWITCHCAPVCC, 0, i == 0, true); // Reset nur beim ersten Display aktiv
    oleds[i]->setRotation(0);
    oleds[i]->clearDisplay();
  }

  USBComposite.setProductId(PRODUCT_ID);
  HID.registerComponent();
  CompositeSerial.registerComponent();
  USBComposite.begin();

  displayContrast(LOW);

  // Standard-Bitmaps setzen
  const uint8_t* bitmaps[] = {bmp_mute, bmp_volume_down, bmp_volume_up, bmp_backward,
                              bmp_play, bmp_forward, bmp_explorer, bmp_snapshot, bmp_calc};
  for (uint8_t i = 0; i < 9; i++) {
    oleds[i]->drawBitmap(32, 0, bitmaps[i], 64, 48, WHITE);
  }

  CompositeSerial.setTimeout(200);
}

void loop() {
  for (uint8_t i = 0; i < 9; i++) {
    buttons[i].read();
    if (buttons[i].wasPressed() || buttons[i].pressedFor(300)) {
      handleButtonPress(i);
    }
  }

  handleSerialInput();

  for (uint8_t i = 0; i < 9; i++) {
    oleds[i]->display();
  }
}

void handleButtonPress(uint8_t buttonIndex) {
  if (defaultLayoutActive) {
    switch (buttonIndex) {
      case 0: Consumer.press(HIDConsumer::MUTE); break;
      case 1: Consumer.press(HIDConsumer::VOLUME_DOWN); break;
      case 2: Consumer.press(HIDConsumer::VOLUME_UP); break;
      case 3: Consumer.press(182); break;
      case 4: Consumer.press(HIDConsumer::PLAY_OR_PAUSE); break;
      case 5: Consumer.press(181); break;
      case 6: sendKeyboardShortcut(KEY_LEFT_GUI, 'e'); break;
      case 7: sendKeyboardShortcut(KEY_LEFT_GUI | KEY_LEFT_SHIFT, 's'); break;
      case 8: openCalculator(); break;
    }
    Consumer.release();
  } else {
    CompositeSerial.print(buttonIndex + 1);
  }
}

void handleSerialInput() {
  while (CompositeSerial.available() > 0) {
    char command = CompositeSerial.read();
    if (command >= '0' && command <= '8') {
      defaultLayoutActive = false;
      CompositeSerial.readBytes(bmp_swap, 384);
      uint8_t displayIndex = command - '0';
      oleds[displayIndex]->clearDisplay();
      oleds[displayIndex]->drawBitmap(32, 0, bmp_swap, 64, 48, WHITE);
    } else if (command == 'D') {
      resetToDefaultLayout();
    } else if (command == 'B') {
      displayContrast(HIGH);
    } else if (command == 'b') {
      displayContrast(LOW);
    }
  }
}

void resetToDefaultLayout() {
  defaultLayoutActive = true;
  const uint8_t* bitmaps[] = {bmp_mute, bmp_volume_down, bmp_volume_up, bmp_backward,
                              bmp_play, bmp_forward, bmp_explorer, bmp_snapshot, bmp_calc};
  for (uint8_t i = 0; i < 9; i++) {
    oleds[i]->clearDisplay();
    oleds[i]->drawBitmap(32, 0, bitmaps[i], 64, 48, WHITE);
  }
}

void sendKeyboardShortcut(uint8_t modifiers, char key) {
  Keyboard.press(modifiers);
  Keyboard.press(key);
  Keyboard.release(key);
  Keyboard.release(modifiers);
}

void openCalculator() {
  Keyboard.press(KEY_LEFT_GUI);
  Keyboard.press('r');
  Keyboard.release('r');
  Keyboard.release(KEY_LEFT_GUI);
  Keyboard.print("calc");
  Keyboard.press('\n');
  Keyboard.release('\n');
}

void displayContrast(boolean contrast) {
  uint8_t contrastSetting = contrast == HIGH ? 0x7F : 0x35;
  for (uint8_t i = 0; i < 9; i++) {
    oleds[i]->ssd1306_command(SSD1306_SETCONTRAST);
    oleds[i]->ssd1306_command(contrastSetting);
  }
}
