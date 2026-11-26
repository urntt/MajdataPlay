#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#import "UnityInterface.h"

// 导入谱面：弹出系统文件选择器，任何文件和文件夹都能选（谱面由 ChartImporter 按内容识别，不看后缀）。
// 选中的文件或文件夹在安全范围访问下拷到临时目录 maicharts_inbox，再像“用其他应用打开”一样交给
// GameManager.IOS_OnFileOpen，由 ChartImporter 导入，导入后删除这份副本。
@interface MajDocumentPickerDelegate : NSObject <UIDocumentPickerDelegate>
@end

// The picker only keeps a weak reference to its delegate.
static MajDocumentPickerDelegate* s_pickerDelegate = nil;

@implementation MajDocumentPickerDelegate

- (void)documentPicker:(UIDocumentPickerViewController*)controller didPickDocumentsAtURLs:(NSArray<NSURL*>*)urls
{
    NSFileManager* files = [NSFileManager defaultManager];
    NSString* inbox = [NSTemporaryDirectory() stringByAppendingPathComponent:@"maicharts_inbox"];
    [files createDirectoryAtPath:inbox withIntermediateDirectories:YES attributes:nil error:nil];
    for (NSURL* url in urls)
    {
        BOOL isScoped = [url startAccessingSecurityScopedResource];
        NSString* destination = [inbox stringByAppendingPathComponent:url.lastPathComponent];
        [files removeItemAtPath:destination error:nil];
        NSError* error = nil;
        BOOL isCopied = [files copyItemAtURL:url toURL:[NSURL fileURLWithPath:destination] error:&error];
        if (isScoped)
        {
            [url stopAccessingSecurityScopedResource];
        }
        if (!isCopied)
        {
            NSLog(@"[MajDocumentPicker] copy failed: %@", error);
            continue;
        }
        UnitySendMessage("GameManager", "IOS_OnFileOpen", destination.UTF8String);
    }
    s_pickerDelegate = nil;
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController*)controller
{
    s_pickerDelegate = nil;
}

@end

extern "C" void MajDocumentPicker_Show(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = UnityGetGLViewController();
        if (root == nil || s_pickerDelegate != nil)
        {
            return;
        }
        // Any item, and folders as items to pick rather than only to open; the files are opened in place (asCopy NO)
        // because the system cannot copy a folder, and the delegate copies what was picked itself.
        NSArray<UTType*>* types = @[ UTTypeItem, UTTypeFolder ];
        UIDocumentPickerViewController* picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:types
                                                                                                             asCopy:NO];
        picker.allowsMultipleSelection = YES;
        s_pickerDelegate = [[MajDocumentPickerDelegate alloc] init];
        picker.delegate = s_pickerDelegate;
        [root presentViewController:picker animated:YES completion:nil];
    });
}
