"""Derive MinimalJoystick from the pinned, MS-PL-licensed Microsoft UMDF sample.

Vendor files remain untouched. Generated driver sources are checked by tests.
"""
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'vendor/vhidmini2'
DST = ROOT / 'driver'
DST.mkdir(exist_ok=True)
for source, dest in [('driver/vhidmini.c', 'vhidmini.c'), ('driver/vhidmini.h', 'vhidmini.h'),
                     ('inc/common.h', 'common.h'), ('driver/umdf2/util.c', 'util.c')]:
    shutil.copyfile(SRC / source, DST / dest)

p = DST / 'common.h'
s = p.read_text()
for key, value in [('MANUFACTURER', 'MinimalJoystick'), ('PRODUCT', 'MinimalJoystick'),
                   ('SERIAL_NUMBER', 'MJ-PROTOTYPE-0001'), ('DEVICE', 'MinimalJoystick')]:
    s = re.sub(r'(#define VHIDMINI_' + key + r'_STRING\s+)L"[^"]*"', r'\g<1>L"' + value + '"', s)
s += '''
// MinimalJoystick v1: one joystick TLC, two axes + four buttons.
// IDs are local prototype values, not a registered USB vendor allocation.
#include <pshpack1.h>
typedef struct { UCHAR ReportId; USHORT Rudder; USHORT Throttle; UCHAR Buttons; } MJ_INPUT;
typedef struct { UCHAR ReportId; UCHAR Version; USHORT Rudder; USHORT Throttle; UCHAR Buttons; UCHAR Reserved; } MJ_FEATURE;
#include <poppack.h>
'''
p.write_text(s)
p = DST / 'vhidmini.h'
s = p.read_text().replace('BYTE                    DeviceData;',
    'MJ_INPUT                State;\n    ULONGLONG               LastFeed;\n    WDFWAITLOCK             StateLock;')
s = s.replace('0xFEED', '0x4D4A').replace('0xDEED', '0x1209')
p.write_text(s)
p = DST / 'vhidmini.c'
s = p.read_text()
descriptor = '''HID_REPORT_DESCRIPTOR G_DefaultReportDescriptor[] = {
    0x05,0x01, 0x09,0x04, 0xA1,0x01, // Generic Desktop / Joystick
    0x85,0x01, 0x15,0x00, 0x26,0xFF,0x7F,
    0x75,0x10, 0x95,0x02, 0x09,0x35, 0x09,0x36, // Rz, Slider
    0x81,0x02,
    0x05,0x09, 0x19,0x01, 0x29,0x04, 0x15,0x00, 0x25,0x01,
    0x75,0x01, 0x95,0x04, 0x81,0x02,
    0x75,0x04, 0x95,0x01, 0x81,0x03, // button padding
    0x06,0x00,0xFF, 0x09,0x01, 0x85,0x02,
    0x15,0x00, 0x26,0xFF,0x00, 0x75,0x08, 0x95,0x07, 0xB1,0x02,
    0xC0
};'''
s = re.sub(r'HID_REPORT_DESCRIPTOR\s+G_DefaultReportDescriptor\[\] = \{.*?\};', descriptor, s, count=1, flags=re.S)
s = s.replace('deviceContext->DeviceData   = 0;', '''deviceContext->State.ReportId = 1;
    deviceContext->State.Rudder = 16384;
    deviceContext->LastFeed = 0;
    {
        WDF_OBJECT_ATTRIBUTES lockAttributes;
        WDF_OBJECT_ATTRIBUTES_INIT(&lockAttributes);
        lockAttributes.ParentObject = device;
        status = WdfWaitLockCreate(&lockAttributes, &deviceContext->StateLock);
        if (!NT_SUCCESS(status)) return status;
    }''')
start = s.index('    status = CheckRegistryForDescriptor(device);')
end = s.index('    return status;', start)
s = s[:start] + '    deviceContext->ReportDescriptor = G_DefaultReportDescriptor;\n    status = STATUS_SUCCESS;\n\n' + s[end:]

def function(name, next_name, body):
    global s
    start = re.search(r'(?:NTSTATUS|HRESULT)\n' + name + r'\(', s).start()
    end = re.search(r'(?:NTSTATUS|HRESULT)\n' + next_name + r'\(', s[start+1:]).start() + start + 1
    s = s[:start] + body + '\n\n' + s[end:]

function('WriteReport', 'GetFeature', '''NTSTATUS
WriteReport(_In_ PQUEUE_CONTEXT QueueContext, _In_ WDFREQUEST Request)
{ UNREFERENCED_PARAMETER(QueueContext); UNREFERENCED_PARAMETER(Request); return STATUS_NOT_SUPPORTED; }''')
function('GetFeature', 'SetFeature', '''NTSTATUS
GetFeature(_In_ PQUEUE_CONTEXT QueueContext, _In_ WDFREQUEST Request)
{
    HID_XFER_PACKET packet;
    MJ_FEATURE result = {2, 1, 16384, 0, 0, 0};
    NTSTATUS status = RequestGetHidXferPacket_ToReadFromDevice(Request, &packet);
    if (!NT_SUCCESS(status)) return status;
    if (packet.reportId != 2 || packet.reportBufferLen != sizeof(result)) return STATUS_INVALID_PARAMETER;
    WdfWaitLockAcquire(QueueContext->DeviceContext->StateLock, NULL);
    result.Rudder = QueueContext->DeviceContext->State.Rudder;
    result.Throttle = QueueContext->DeviceContext->State.Throttle;
    result.Buttons = QueueContext->DeviceContext->State.Buttons;
    WdfWaitLockRelease(QueueContext->DeviceContext->StateLock);
    RtlCopyMemory(packet.reportBuffer, &result, sizeof(result));
    WdfRequestSetInformation(Request, sizeof(result));
    return STATUS_SUCCESS;
}''')
function('SetFeature', 'GetInputReport', '''NTSTATUS
SetFeature(_In_ PQUEUE_CONTEXT QueueContext, _In_ WDFREQUEST Request)
{
    HID_XFER_PACKET packet;
    MJ_FEATURE data;
    PDEVICE_CONTEXT context = QueueContext->DeviceContext;
    NTSTATUS status = RequestGetHidXferPacket_ToWriteToDevice(Request, &packet);
    if (!NT_SUCCESS(status)) return status;
    if (packet.reportId != 2 || packet.reportBufferLen != sizeof(data)) return STATUS_INVALID_PARAMETER;
    RtlCopyMemory(&data, packet.reportBuffer, sizeof(data));
    if (data.ReportId != 2 || data.Version != 1 || data.Reserved != 0 ||
        data.Rudder > 32767 || data.Throttle > 32767 || (data.Buttons & 0xF0)) return STATUS_INVALID_PARAMETER;
    WdfWaitLockAcquire(context->StateLock, NULL);
    context->State.ReportId = 1;
    context->State.Rudder = data.Rudder;
    context->State.Throttle = data.Throttle;
    context->State.Buttons = data.Buttons;
    context->LastFeed = GetTickCount64();
    WdfWaitLockRelease(context->StateLock);
    WdfRequestSetInformation(Request, sizeof(data));
    return STATUS_SUCCESS;
}''')
function('GetInputReport', 'SetOutputReport', '''NTSTATUS
GetInputReport(_In_ PQUEUE_CONTEXT QueueContext, _In_ WDFREQUEST Request)
{
    HID_XFER_PACKET packet;
    NTSTATUS status = RequestGetHidXferPacket_ToReadFromDevice(Request, &packet);
    if (!NT_SUCCESS(status)) return status;
    if (packet.reportId != 1 || packet.reportBufferLen < sizeof(MJ_INPUT)) return STATUS_INVALID_PARAMETER;
    WdfWaitLockAcquire(QueueContext->DeviceContext->StateLock, NULL);
    RtlCopyMemory(packet.reportBuffer, &QueueContext->DeviceContext->State, sizeof(MJ_INPUT));
    WdfWaitLockRelease(QueueContext->DeviceContext->StateLock);
    WdfRequestSetInformation(Request, sizeof(MJ_INPUT));
    return STATUS_SUCCESS;
}''')
function('SetOutputReport', 'GetStringId', '''NTSTATUS
SetOutputReport(_In_ PQUEUE_CONTEXT QueueContext, _In_ WDFREQUEST Request)
{ UNREFERENCED_PARAMETER(QueueContext); UNREFERENCED_PARAMETER(Request); return STATUS_NOT_SUPPORTED; }''')
s = s.replace('ULONG                   timerPeriodInSeconds = 5;', 'ULONG timerPeriodInMilliseconds = 10;')
s = s.replace('timerPeriodInSeconds * 1000', 'timerPeriodInMilliseconds')
s = s.replace('WDF_REL_TIMEOUT_IN_SEC(1)', 'WDF_REL_TIMEOUT_IN_MS(10)')
s = s.replace('HIDMINI_INPUT_REPORT    readReport;', 'MJ_INPUT readReport;')
s = s.replace('''readReport.ReportId = CONTROL_FEATURE_REPORT_ID;
        readReport.Data     = queueContext->DeviceContext->DeviceData;''', '''WdfWaitLockAcquire(queueContext->DeviceContext->StateLock, NULL);
        // Bridge timeout: release buttons, center rudder, hold last throttle.
        if (GetTickCount64() - queueContext->DeviceContext->LastFeed > 500) {
            queueContext->DeviceContext->State.Rudder = 16384;
            queueContext->DeviceContext->State.Buttons = 0;
        }
        readReport = queueContext->DeviceContext->State;
        WdfWaitLockRelease(queueContext->DeviceContext->StateLock);''')
# Wait locks and timer execute at passive level; serialize callback dispatch.
s = s.replace('deviceAttributes,\n                            DEVICE_CONTEXT);', 'deviceAttributes,\n                            DEVICE_CONTEXT);\n    deviceAttributes.ExecutionLevel = WdfExecutionLevelPassive;')
p.write_text(s)

inf = (SRC / 'driver/umdf2/VhidminiUm.inx').read_text()
inf = inf.replace('VhidminiUm', 'MinimalJoystick').replace('root\\MinimalJoystick', 'root\\MinimalJoystick')
inf = inf.replace('wudf.cat', 'MinimalJoystick.cat').replace('NT$ARCH$', 'NTamd64')
inf = inf.replace('$UMDFVERSION$', '2.15.0').replace('10/01/2002,6.0.5058.0', '11/08/2026,0.1.0.0')
for old in ['TODO-Set-Provider', 'TODO-Set-Manufacturer', 'Sample Device',
            'HID minidriver (UMDF v2) Installation Disk', 'HID minidriver (UMDF v2) Device']:
    inf = inf.replace(old, 'MinimalJoystick')
inf = inf.replace('Needs=MsHidUmdf.NT.hw', 'Needs=MsHidUmdf.NT.hw\nAddReg=MinimalJoystick.Branding')
inf += '''
[MinimalJoystick.Branding]
HKR,,FriendlyName,0,"MinimalJoystick"
'''
(DST / 'MinimalJoystick.inf').write_text(inf)
print('Generated branded UMDF driver source; compilation and installation are separate steps.')
