using System;
using System.Collections.Generic;
using System.Linq;

using Smdn.Devices.UsbHid;

namespace Smdn.Devices.MCP2221;


public class MCP2221ADC
{

  static MCP2221ADC()
  {
    @"Chip Definition for MCP2221";
  }

  public float MCP2221_HID_DELAY = (float)(os.environ.get("BLINKA_MCP2221_HID_DELAY", 0));

  public float MCP2221_RESET_DELAY = (float)(os.environ.get("BLINKA_MCP2221_RESET_DELAY", 0.5));

  public const byte RESP_ERR_NOERR = 0x00;

  public const byte RESP_ADDR_NACK = 0x25;

  public const byte RESP_READ_ERR = 0x7F;

  public const byte RESP_READ_COMPL = 0x55;

  public const byte RESP_READ_PARTIAL = 0x54;

  public const byte RESP_I2C_IDLE = 0x00;

  public const byte RESP_I2C_START_TOUT = 0x12;

  public const byte RESP_I2C_RSTART_TOUT = 0x17;

  public const byte RESP_I2C_WRADDRL_TOUT = 0x23;

  public const byte RESP_I2C_WRADDRL_WSEND = 0x21;

  public const byte RESP_I2C_WRADDRL_NACK = 0x25;

  public const byte RESP_I2C_WRDATA_TOUT = 0x44;

  public const byte RESP_I2C_RDDATA_TOUT = 0x52;

  public const byte RESP_I2C_STOP_TOUT = 0x62;

  public const byte RESP_I2C_MOREDATA = 0x43;

  public const byte RESP_I2C_PARTIALDATA = 0x41;

  public const byte RESP_I2C_WRITINGNOSTOP = 0x45;

  public const byte MCP2221_RETRY_MAX = 50;

  public const byte MCP2221_MAX_I2C_DATA_LEN = 60;

  public const byte MASK_ADDR_NACK = 0x40;

  // MCP2221 Device Class Definition
  public class MCP2221 {

    public const int VID = 0x04D8;

    public const int PID = 0x00DD;

    public const int GP_GPIO = 0b000;

    public const int GP_DEDICATED = 0b001;

    public const int GP_ALT0 = 0b010;

    public const int GP_ALT1 = 0b011;

    public const int GP_ALT2 = 0b100;

    public MCP2221()
    {
      this._hid = hid.device();
      this._hid.open(MCP2221.VID, MCP2221.PID);
      // make sure the device gets closed before exit
      atexit.register(this.close);
      if (MCP2221_RESET_DELAY >= 0) {
        this._reset();
      }
      this._gp_config = new List<object> {
                  0x07
              } * 4;
      foreach (var pin in Enumerable.Range(0, 4)) {
        this.gp_set_mode(pin, this.GP_GPIO);
        this.gpio_set_direction(pin, 1);
      }
    }

    // Close the hid device. Does nothing if the device is not open.
    public virtual void Close()
    {
      this._hid.close();
    }

    public virtual void @__del__()
    {
      // try to close the device before destroying the instance
      this.close();
    }

    // Perform HID Transfer
    public virtual byte _hid_xfer(object report, bool response = true)
    {
      // first byte is report ID, which =0 for MCP2221
      // remaing bytes = 64 byte report data
      // https://github.com/libusb/hidapi/blob/083223e77952e1ef57e6b77796536a3359c1b2a3/hidapi/hidapi.h#L185
      this._hid.write(new byte[] { \0 } + report + new byte[] { \0 } * (64 - report.Count));
      time.sleep(MCP2221_HID_DELAY);
      if (response) {
        // return is 64 byte response report
        return this._hid.read(64);
      }
      return null;
    }

    // ----------------------------------------------------------------
    // MISC
    // ----------------------------------------------------------------
    // Get Current Pin Mode
    public virtual object gp_get_mode(object pin)
    {
      return this._hid_xfer(new byte[] { 0x61 })[22 + pin] & 0x07;
    }

    // Set Current Pin Mode
    public virtual object gp_set_mode(object pin, object mode)
    {
      // already set to that mode?
      mode |= 0x07;
      if (mode == (this._gp_config[pin] & 0x07)) {
        return;
      }
      // update GP mode for pin
      this._gp_config[pin] = mode;
      // empty report, this is safe since 0's = no change
      var report = bytearray(new byte[] { 0x60 } + new byte[] { 0x00 } * 63);
      // set the alter GP flag byte
      report[7] = 0xFF;
      // add GP setttings
      report[8] = this._gp_config[0];
      report[9] = this._gp_config[1];
      report[10] = this._gp_config[2];
      report[11] = this._gp_config[3];
      // and make it so
      this._hid_xfer(report);
    }

    public virtual object _pretty_report(object register)
    {
      var report = this._hid_xfer(register);
      Console.WriteLine("     0  1  2  3  4  5  6  7  8  9");
      var index = 0;
      foreach (var row in Enumerable.Range(0, 7)) {
        Console.WriteLine("{} : ".format(row), end: "");
        foreach (var _ in Enumerable.Range(0, 10)) {
          Console.WriteLine("{:02x} ".format(report[index]), end: "");
          index += 1;
          if (index > 63) {
            break;
          }
        }
        Console.WriteLine();
      }
    }

    public virtual object _status_dump()
    {
      this._pretty_report(new byte[] { 0x10 });
    }

    public virtual object _sram_dump()
    {
      this._pretty_report(new byte[] { 0x61 });
    }

    public virtual object _reset()
    {
      this._hid_xfer(new byte[] { 0x70, 0xAB, 0xCD, 0xEF }, response: false);
      this._hid.close();
      time.sleep(MCP2221_RESET_DELAY);
      var start = time.monotonic();
      while (time.monotonic() - start < 5) {
        try {
          this._hid.open(MCP2221.VID, MCP2221.PID);
        }
        catch (OSError) {
          // try again
          time.sleep(0.1);
          continue;
        }
        return;
      }
      throw OSError("open failed");
    }

    // ----------------------------------------------------------------
    // GPIO
    // ----------------------------------------------------------------
    // Set Current GPIO Pin Direction
    public virtual object gpio_set_direction(object pin, object mode)
    {
      if (mode) {
        // set bit 3 for INPUT
        this._gp_config[pin] |= 1 << 3;
      }
      else {
        // clear bit 3 for OUTPUT
        this._gp_config[pin] |= ~(1 << 3);
      }
      var report = bytearray(new byte[] { 0x50 } + new byte[] { 0x00 } * 63);
      var offset = 4 * (pin + 1);
      report[offset] = 0x01;
      report[offset + 1] = mode;
      this._hid_xfer(report);
    }

    // Set Current GPIO Pin Value
    public virtual object gpio_set_pin(object pin, object value)
    {
      if (value) {
        // set bit 4
        this._gp_config[pin] |= 1 << 4;
      }
      else {
        // clear bit 4
        this._gp_config[pin] |= ~(1 << 4);
      }
      var report = bytearray(new byte[] { 0x50 } + new byte[] { 0x00 } * 63);
      var offset = 2 + 4 * pin;
      report[offset] = 0x01;
      report[offset + 1] = value;
      this._hid_xfer(report);
    }

    // Get Current GPIO Pin Value
    public virtual object gpio_get_pin(object pin)
    {
      var resp = this._hid_xfer(new byte[] { 0x51 });
      var offset = 2 + 2 * pin;
      if (resp[offset] == 0xEE) {
        throw RuntimeError("Pin is not set for GPIO operation.");
      }
      return resp[offset];
    }

    // ----------------------------------------------------------------
    // I2C
    // ----------------------------------------------------------------
    public virtual object _i2c_status()
    {
      var resp = this._hid_xfer(new byte[] { 0x10 });
      if (resp[1] != 0) {
        throw RuntimeError("Couldn't get I2C status");
      }
      return resp;
    }

    public virtual object _i2c_state()
    {
      return this._i2c_status()[8];
    }

    public virtual object _i2c_cancel()
    {
      var resp = this._hid_xfer(new byte[] { 0x10, 0x00, 0x10 });
      if (resp[1] != 0x00) {
        throw RuntimeError("Couldn't cancel I2C");
      }
      if (resp[2] == 0x10) {
        // bus release will need "a few hundred microseconds"
        time.sleep(0.001);
      }
    }

    // pylint: disable=too-many-arguments,too-many-branches
    public virtual object _i2c_write(
        object cmd,
        object address,
        object buffer,
        object start = 0,
        object end = null)
    {
      if (this._i2c_state() != 0x00) {
        this._i2c_cancel();
      }
      end = end ? end : buffer.Count;
      var length = end - start;
      var retries = 0;
      while (end - start > 0 || !buffer) {
        var chunk = min(end - start, MCP2221_MAX_I2C_DATA_LEN);
        // write out current chunk
        var resp = this._hid_xfer(bytes(new List<object> {
                      cmd,
                      length & 0xFF,
                      length >> 8 & 0xFF,
                      address << 1
                  }) + buffer[start::(start  +  chunk)]);
        // check for success
        if (resp[1] != 0x00) {
          if ((RESP_I2C_START_TOUT, RESP_I2C_WRADDRL_TOUT, RESP_I2C_WRADDRL_NACK, RESP_I2C_WRDATA_TOUT, RESP_I2C_STOP_TOUT).Contains(resp[2])) {
            throw RuntimeError("Unrecoverable I2C state failure");
          }
          retries += 1;
          if (retries >= MCP2221_RETRY_MAX) {
            throw RuntimeError("I2C write error, max retries reached.");
          }
          time.sleep(0.001);
          continue;
        }
        // yay chunk sent!
        while (this._i2c_state() == RESP_I2C_PARTIALDATA) {
          time.sleep(0.001);
        }
        if (!buffer) {
          break;
        }
        start += chunk;
        retries = 0;
      }
      // check status in another loop
      foreach (var _ in Enumerable.Range(0, MCP2221_RETRY_MAX)) {
        var status = this._i2c_status();
        if (status[20] & MASK_ADDR_NACK) {
          throw RuntimeError("I2C slave address was NACK'd");
        }
        var usb_cmd_status = status[8];
        if (usb_cmd_status == 0) {
          break;
        }
        if (usb_cmd_status == RESP_I2C_WRITINGNOSTOP && cmd == 0x94) {
          break;
        }
        if ((RESP_I2C_START_TOUT, RESP_I2C_WRADDRL_TOUT, RESP_I2C_WRADDRL_NACK, RESP_I2C_WRDATA_TOUT, RESP_I2C_STOP_TOUT).Contains(usb_cmd_status)) {
          throw RuntimeError("Unrecoverable I2C state failure");
        }
        time.sleep(0.001);
      }
    }

    public virtual object _i2c_read(
        object cmd,
        object address,
        object buffer,
        object start = 0,
        object end = null)
    {
      if (!(RESP_I2C_WRITINGNOSTOP, 0).Contains(this._i2c_state())) {
        this._i2c_cancel();
      }
      end = end ? end : buffer.Count;
      var length = end - start;
      // tell it we want to read
      var resp = this._hid_xfer(bytes(new List<object> {
                  cmd,
                  length & 0xFF,
                  length >> 8 & 0xFF,
                  address << 1 | 0x01
              }));
      // check for success
      if (resp[1] != 0x00) {
        throw RuntimeError("Unrecoverable I2C read failure");
      }
      // and now the read part
      while (end - start > 0) {
        foreach (var _ in Enumerable.Range(0, MCP2221_RETRY_MAX)) {
          // the actual read
          resp = this._hid_xfer(new byte[] { 0x40 });
          // check for success
          if (resp[1] == RESP_I2C_PARTIALDATA) {
            time.sleep(0.001);
            continue;
          }
          if (resp[1] != 0x00) {
            throw RuntimeError("Unrecoverable I2C read failure");
          }
          if (resp[2] == RESP_ADDR_NACK) {
            throw RuntimeError("I2C NACK");
          }
          if (resp[3] == 0x00 && resp[2] == 0x00) {
            break;
          }
          if (resp[3] == RESP_READ_ERR) {
            time.sleep(0.001);
            continue;
          }
          if ((RESP_READ_COMPL, RESP_READ_PARTIAL).Contains(resp[2])) {
            break;
          }
        }
        // move data into buffer
        var chunk = min(end - start, 60);
        foreach (var _tup_1 in Enumerable.Range(start, start + chunk - start).Select((_p_1, _p_2) => Tuple.Create(_p_2, _p_1))) {
          var i = _tup_1.Item1;
          var k = _tup_1.Item2;
          buffer[k] = resp[4 + i];
        }
        start += chunk;
      }
    }

    // pylint: enable=too-many-arguments
    // Configure I2C
    public virtual object _i2c_configure(object baudrate = 100000)
    {
      this._hid_xfer(bytes(new List<object> {
                  0x10,
                  0x00,
                  0x00,
                  0x20,
                  12000000 / baudrate - 3
              }));
    }

    // Write data from the buffer to an address
    public virtual object i2c_writeto(object address, object buffer, object start = 0, object end = null)
    {
      this._i2c_write(0x90, address, buffer, start, end);
    }

    // Read data from an address and into the buffer
    public virtual object i2c_readfrom_into(object address, object buffer, object start = 0, object end = null)
    {
      this._i2c_read(0x91, address, buffer, start, end);
    }

    // Write data from buffer_out to an address and then
    //         read data from an address and into buffer_in
    //
    public virtual object i2c_writeto_then_readfrom(
        object address,
        object out_buffer,
        object in_buffer,
        object out_start = 0,
        object out_end = null,
        object in_start = 0,
        object in_end = null)
    {
      this._i2c_write(0x94, address, out_buffer, out_start, out_end);
      this._i2c_read(0x93, address, in_buffer, in_start, in_end);
    }

    // Perform an I2C Device Scan
    public virtual object i2c_scan(object start = 0, object end = 0x79)
    {
      var found = new List<object>();
      foreach (var addr in Enumerable.Range(start, end + 1 - start)) {
        // try a write
        try {
          this.i2c_writeto(addr, new byte[] { 0x00 });
        }
        catch (RuntimeError) {
          // no reply!
          continue;
        }
        found.append(addr);
      }
      return found;
    }

    // ----------------------------------------------------------------
    // ADC
    // ----------------------------------------------------------------
    // Configure the Analog-to-Digital Converter
    public virtual object adc_configure(object vref = 0)
    {
      var report = bytearray(new byte[] { 0x60 } + new byte[] { 0x00 } * 63);
      report[5] = 1 << 7 | vref & 0b111;
      this._hid_xfer(report);
    }

    // Read from the Analog-to-Digital Converter
    public virtual object adc_read(object pin)
    {
      var resp = this._hid_xfer(new byte[] { 0x10 });
      return resp[49 + 2 * pin] << 8 | resp[48 + 2 * pin];
    }

    // ----------------------------------------------------------------
    // DAC
    // ----------------------------------------------------------------
    // Configure the Digital-to-Analog Converter
    public virtual object dac_configure(object vref = 0)
    {
      var report = bytearray(new byte[] { 0x60 } + new byte[] { 0x00 } * 63);
      report[3] = 1 << 7 | vref & 0b111;
      this._hid_xfer(report);
    }

    // pylint: disable=unused-argument
    // Write to the Digital-to-Analog Converter
    public virtual object dac_write(object pin, object value)
    {
      var report = bytearray(new byte[] { 0x60 } + new byte[] { 0x00 } * 63);
      report[4] = 1 << 7 | value & 0b11111;
      this._hid_xfer(report);
      // pylint: enable=unused-argument
    }
  }

  public static object mcp2221 = MCP2221();
}
