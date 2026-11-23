#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#import "UnityInterface.h"

// 导入谱面：弹出系统文件选择器（.zip、.adx、.pez）。选中的文件以副本形式拷到临时目录，
// 再像“用其他应用打开”一样交给 GameManager.IOS_OnFileOpen，由 ChartImporter 导入。
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
        NSMutableArray<UTType*>* types = [NSMutableArray arrayWithObject:UTTypeZIP];
        for (NSString* extension in @[ @"adx", @"pez" ])
        {
            UTType* type = [UTType typeWithFilenameExtension:extension];
            if (type != nil)
            {
                [types addObject:type];
            }
        }
        UIDocumentPickerViewController* picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:types
                                                                                                             asCopy:YES];
        picker.allowsMultipleSelection = YES;
        s_pickerDelegate = [[MajDocumentPickerDelegate alloc] init];
        picker.delegate = s_pickerDelegate;
        [root presentViewController:picker animated:YES completion:nil];
    });
}
