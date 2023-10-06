// SPDX-FileCopyrightText: 2021 smdn <smdn@smdn.jp>
// SPDX-License-Identifier: MIT

using System.Threading;
using System.Threading.Tasks;

namespace Smdn.Devices.MCP2221;

#pragma warning disable IDE0040
partial class MCP2221 {
#pragma warning restore IDE0040
  internal interface IADCFunctionality {
    ValueTask ConfigureAsADCAsync(
      CancellationToken cancellationToken = default
    );
    void ConfigureAsADC(
      CancellationToken cancellationToken = default
    );
    void RetrieveADCData(
      CancellationToken cancellationToken = default
    );

#if __FUTURE_VERSION
    int ADCValue { get; }
#endif
  }
}
